# AMR 관제 시스템 기능명세서

2026-09-30, 버전0.1. [PRD](prd.md)를 개발·검증 단위로 구체화한다. COM-01~04는 적용 중 계약, COM-05는 미착수 범위, AMR/Core/GUI는 후속 제안이다. 제품 상태·합격 기준의 원본은 [기능 추적기](../feature_list.json)다.

## 1. 용어와 완료 단계

| 용어 | 의미 |
| --- | --- |
| Command 발행 성공 | MQTT 발행 호출 완료. AMR 수신·실행 완료와 별개 |
| Command 수신 성공 | AMR 메시지 도착. 형식 검사와 실행 결과는 추가 확인 |
| ACK | cmdId에 대한 SUCCESS/FAILURE 결과. MQTT 전송 계층 응답과 별개 |
| 현재 FAILURE | 정상 명령도 실제 동작 미구현으로 SIMULATION_NOT_IMPLEMENTED 반환 |
| Telemetry | 위치·속도·상태. 관제 수신 구현, 가상 AMR 생성 미구현 |
| Alarm | 장애 발생·해제. 관제 수신 구현, 가상 AMR 장애 생성 미구현 |
| Heartbeat Timeout | 정상 생존 신호 수신 이후 시간 초과. 최종 Offline과 별개 |

## 2. 요구사항과 기능 연결

| 기능 | 요구사항 | revision과 상태 | 검증 수 |
| --- | --- | --- | --- |
| COM-03 | FR-COM-01 | 2 passing | 5 |
| COM-04 | FR-COM-02 | 4 passing | 8 |
| COM-01 | FR-COM-03 | 4 passing | 10 |
| COM-02 | FR-COM-04 | 2 passing | 7 |
| COM-05 | FR-COM-05 | 1 not_started | 7 |

합계37개. S024 결과를 반영한 스냅샷으로 이번 문서 작성의 제품 재실행 결과가 아니다.

## 3. COM-03 Broker 연결과 감시

| 항목 | 명세 |
| --- | --- |
| 담당 | MqttConnectionManager/MqttClientService |
| 입력·선행 조건 | Broker 설정, 연결·종료 요청, 취소 토큰. 실제 Broker 또는 실패 시험 주소 |
| 처리 | 시도별 클라이언트 소유 → 연결 결과 → KeepAlive → 단절 감지 → 자원 정리 |
| 출력 | 연결 상태·이유·시각. 호출자의 명시적 재연결 지원 |
| 예외 | 최초 실패, Broker 종료, 응답 차단, 취소, 이전 세션의 늦은 이벤트 |
| 개발값 | localhost:1883, MQTT5/TCP, KeepAlive5초, 연결/통신 timeout3초 |
| 인수 기준 | 실제 연결·idle·단절2종·최초 실패·재시작 등 추적기5항목 |

자동 재접속·재구독은 통과 범위 밖이다. COM-03 실제 시험은 격리 포트·별도 시간 설정을 사용하므로 개발 기본값과 구분한다.

## 4. COM-04 수신 메시지 검증

| 항목 | 명세 |
| --- | --- |
| 담당 | MessageValidator/PayloadValidator/IncomingMessageProcessor |
| 입력 | 수신 Topic, UTF-8 Payload |
| 처리 | Topic → JSON 구조 → 필수 필드·자료형·범위 → Payload ID와 Topic ID 비교 |
| 출력 | 유효한 불변 모델 또는 오류 코드·필드·이유 |
| 실패 | 검증 로그 기록, Core 미전달, 이후 수신 계속 |
| 제한 | strict UTF-8 JSON object, 최대65,536bytes/깊이16, 중복·추가 필드 거부 |
| 인수 기준 | 네 유형 정상/오류·보존·로그·계속 수신 등 추적기8항목 |

ACK id는 선택이며 있을 때 Topic ID와 비교한다. 모든 Payload에 type/id를 일괄 요구하지 않는다. 검증 로그와 업무 History를 구분한다.

## 5. COM-01 송수신과 AMR별 큐

| 항목 | 명세 |
| --- | --- |
| 담당 | MqttSubscriber/MqttPublisher/AmrMessageDispatcher |
| 입력 | 네 수신 유형, 대상 AMR과 Command Payload |
| 수신 | 검증 → AMR별 bounded Channel → 단일 소비자가 Core 순차 await |
| 발행 | 대상 Topic·Command 검사 → 원래 ID/Payload로 발행 |
| 순서·독립성 | 같은 AMR 네 유형은 한 큐에서 도착 순서 처리, 다른 AMR은 독립 |
| 제한 | 기본 AMR당256대기/세션64AMR, QoS0/Retain false |
| 예외 | QueueFull/AmrLimitReached 신규 거부, HandlerFailed 기록 후 다음 처리 |
| 종료 | 새 수신 차단, 처리 취소 요청, 대기 폐기·기록, 제한 시간 내 종료 대기 |
| 인수 기준 | 송수신·순서·독립성·과부하·종료 등 추적기10항목 |

취소 무시 Core 작업은 강제 중단하지 않는다. 실제 Core는 동시 호출·취소에 협조해야 한다. 상세는 [큐 계약](../src/Amr.Communication/Mqtt/RECEIVE_QUEUE.md)을 따른다.

## 6. COM-02 Heartbeat 감시

| 항목 | 명세 |
| --- | --- |
| 담당 | HeartbeatMonitor/IHeartbeatEventSink |
| 시작 | 현재 세션에서 첫 정상 Heartbeat를 받은 허용 AMR |
| 입력 | 검증 직전 캡처한 단조 시각·관제 UTC와 검증 성공 Heartbeat |
| 처리 | 큐 대기 전 수신 시각 갱신, elapsed >= timeout 검사, 구간당1회 알림 |
| 출력 | MonitoringStarted/TimedOut/Recovered, AMR·수신/관측 시각·경과/제한 시간 |
| 개발값 | Timeout5초/확인500ms, 설정 가능 |
| 예외 | 불량 Heartbeat·다른 유형은 갱신 안 함. QueueFull 정상 Heartbeat는 갱신 |
| 복구·종료 | 정상 재수신 복구1회, 감지된 Broker 단절/종료 시 감시 중단, 새 세션은 새 감시 |
| 인수 기준 | 수신·시간 경계·독립성·오류·복구 등 추적기7항목 |

Payload ts·Core 처리 시각은 생존 판정에 쓰지 않는다. 통상5~5.5초에 OS 지연이 추가될 수 있다. 제어 시계의 정확한 경계 시험과 실제 시험 여유를 구분한다. 미수신 등록 장비·Offline·MqttAlive는 P-06 후속이다.

## 7. COM-05 요청과 ACK 연계

기본 왕복·ID 보존 코드는 있으나 기능 전체는 not_started이다. 다음 반복 개발에서 DN-06을 기록하고 전체 증거를 확보한다.

| 항목 | 명세 |
| --- | --- |
| 요구사항 | FR-COM-05, SC-002 |
| 책임 제안 | Core 대역은 ID 생성·요청 보관·매핑, Communication은 전달·보존 |
| 입력 | 대상 AMR, 고유 cmdId, MOVE/STOP/RESET, ts, param |
| 선행 조건 | COM-01 passing, 실제 Mosquitto, 시험 AMR2대, Core 대역 |
| 처리 | 요청 보관 → 검사/발행 → AMR 수신 → 같은 ID ACK → 검사/큐 → Core에서 AMR+cmdId 연결 |
| 출력 | AMR/cmdId/result/ts와 선택 reason 보존, 단계별 추적 기록 |
| 오류 | 불량 ACK 거부, 유효 FAILURE 그대로 전달. 미요청·중복 등의 업무 정책은 CORE-04 |
| 인수 기준 | 추적기 verification1~7 모두. [Iteration01](iteration_01.md)에 일대일 대응 |

대역의 SUCCESS는 실제 이동 성공 증거가 아니다. ACK 미수신을 성공으로 판단하지 않는다. 실제 업무 상태 저장·ACK Timeout은 별도 후속 범위다.

## 8. 메시지 계약

Topic은 대소문자를 구분한다. AMR ID에 빈 값·공백·제어문자·`/`·`+`·`#`를 허용하지 않는다. ts는 signed Int64 Unix milliseconds 정수이며 문자열·소수·지수 표기를 거부한다.

| 유형 | 방향과 Topic | 필수 | 선택 |
| --- | --- | --- | --- |
| Command | 관제 → AMR, amr/{id}/command | cmdId/type/ts/param | 없음 |
| ACK | AMR → 관제, amr/{id}/ack | cmdId/result/ts | id/reason |
| Heartbeat | AMR → 관제, amr/{id}/heartbeat | id/ts/status | 없음 |
| Telemetry | AMR → 관제, amr/{id}/telemetry | id/ts/pos{x,y}/sp/state | 없음 |
| Alarm | AMR → 관제, amr/{id}/alarm | id/ts/code/level/alarmState | message |

| 필드 | 현재 허용값 |
| --- | --- |
| Command | MOVE param은 유한 숫자 x/y, STOP·RESET param은 빈 object |
| ACK | SUCCESS/FAILURE, reason은 있으면 공백 아닌 문자열 |
| Heartbeat | status=OK |
| Telemetry | x/y 유한 숫자, sp 유한·0이상, IDLE/BUSY/STOPPED/FAULT |
| Alarm | INFO/WARN/ERR/FATAL, ACTIVE/CLEARED |

아래는 형식 예시로 각 줄은 별도 메시지다. 실행 결과가 아니다.

```json
{"cmdId":"CMD-example-001","type":"MOVE","ts":2208988800123,"param":{"x":30,"y":15}}
{"id":"AMR_001","cmdId":"CMD-example-001","result":"FAILURE","reason":"SIMULATION_NOT_IMPLEMENTED","ts":2208988800124}
{"id":"AMR_001","ts":2208988800125,"status":"OK"}
{"id":"AMR_001","ts":2208988800126,"pos":{"x":1.25,"y":2.5},"sp":0.8,"state":"BUSY"}
{"id":"AMR_001","ts":2208988800127,"code":"ALM-EXAMPLE","level":"WARN","alarmState":"ACTIVE","message":"시험용 장애"}
```

읽을 수 없는 Command 또는 없거나 잘못되거나 중복된 cmdId에는 임의 ACK를 만들지 않는다. 유효하고 유일한 cmdId가 있는 거부 요청은 같은 ID의 FAILURE를 반환한다. TestClient의 실패 응답은 reason을 넣고, 수신 측은 기존 reason 없는 ACK도 허용한다. 원본은 [수신 계약](../src/Amr.Communication/Validation/CONTRACT.md)과 [명령 계약](../src/Amr.Communication/Command/CONTRACT.md)이다.

## 9. 후속 기능 상세 초안

아래는 아직 채택되지 않은 후속 명세다. PRD의 P 항목을 정리하고 기능 추적기에 범위·검증을 등록한 뒤 구현한다.

| 기능 | 입력과 조건 | 처리·출력 | 예외와 수용 기준 | 결정 |
| --- | --- | --- | --- | --- |
| AMR 설정 | ID·위치·경로·속도·초기 상태 | 독립 상태 초기화·연결 | 불량 설정 거부, 2대 상태 독립 | P-07 |
| MOVE·Telemetry | 허용 상태·목표 x/y | 이동 계산·주기 상태 발행·완료 결과 | 내부/수신 값 일치, 거부 사유, 완료 전 성공 없음 | P-02/03/07 |
| STOP | 이동 중 정지 요청 | 이동 중지·STOPPED 목표, 두 요청 결과 | 좌표 고정, 원래 MOVE 결과 확정 필요 | P-02/03 |
| 장애·Alarm | 장애 발생·원인 해소 | ACTIVE/CLEARED·상태·이동 제한 | 실제 장애와 이벤트 일치, 파서의 임의 상태 변경 없음 | P-04 |
| RESET | 허용 상태·복구 요청 | 원인 확인·복구 결과 | 원인 지속 실패, 성공 전이·해제 순서 명시 | P-03/04 |
| 자동 복구 | 감지된 Broker 단절 | 재시도·재연결·재구독·새 통신 | 취소 가능, 이전 Command 무조건 재실행 금지 | P-05 |
| Core 상태 | 등록·메시지·감시 이벤트 | 현재 관측값·통신/운행 상태 | 미등록·미수신·시각 역전 정책 | P-06 |
| Core 명령 | 요청·ACK·업무 시계 | 요청 저장·상태 전이·미수신 판정 | 중복·미요청·타 AMR·늦은 ACK 정책 | P-08 |
| SQLite | Core 업무 기록 | 저장·조회 | 재시작 보존·쓰기 실패·보관 정책 | P-09 |
| GUI | Core 조회·알림·사용자 조작 | 장비·위치·장애·결과 표시 | 입력 오류 표시, Core 값 일치, 직접 MQTT/DB 접근 없음 | P-09 |

## 10. 상태 전이의 결정 경계

현재 TestClient에는 운행 상태 머신이 없다. 아래는 기존 과제 계획의 목표이며 미정 행은 구현 전에 확정한다.

| 시작 | 사건 | 목표 또는 미정 |
| --- | --- | --- |
| IDLE | 허용 MOVE | BUSY로 이동 시작 |
| BUSY | 도착 | IDLE 및 원래 MOVE 완료 결과 |
| BUSY | STOP | STOPPED 목표, 원래 MOVE 종료 결과 P-02 |
| STOPPED | MOVE/RESET | 재출발·허용 명령 P-03 |
| 임의 상태 | 장애 | code/level별 상태·제한 P-04 |
| FAULT | 원인 지속 RESET | 실패 목표, 상세 상태 P-04 |
| FAULT | 원인 해소 RESET | 성공 후 상태·Alarm 해제 순서 P-03/04 |

HeartbeatTimeout이나 Alarm 수신만으로 임의 운행 전이를 만들지 않는다. 통신·운행·명령 업무 상태는 별개로 관리한다.

## 11. 검증과 완료

제품 변경 시 `./init.cmd verify`와 해당 실제 Broker·회귀 검증을 수행한다. 종료 전 tracker 검사도 한다. 단위 테스트만으로 실제 MQTT 통과를 주장하지 않는다.

증거 필드는 spec_revision, verification_index, executed_at, command_or_procedure, environment, expected, actual, result, artifact_paths다. 실패·이전 revision 이력을 보존하고 현재 합격 근거와 구분한다. 후속 기준은 채택 시 추적기에 먼저 반영한다.
