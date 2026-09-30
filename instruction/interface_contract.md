# AMR 공통 데이터와 인터페이스 명세

버전0.2, 2026-09-30. 요구사항·기능명세와 함께 전달하는 논리 계약이다. 기존 코드를 읽어야만 해석할 수 있는 필드를 남기지 않는다. **원문**, **기존 결정**, **신규 제안**을 구분한다. 신규 제안은 P 결정을 채택한 후 해당 기능의 구현·인수 기준으로 사용한다. 실제 클래스·메서드·HTTP 경로·DB 테이블을 지정하는 문서가 아니다.

## 1. 식별과 시간

[1차 MVP 계획](happy_path_plan.md)은 이 문서의 Telemetry·MOVE·ACK와 최소 Core/GUI 인터페이스만 먼저 적용한다. 저장 모델·Alarm·업무 Timeout 등은 2차이며 아래 전체 계약을 삭제하거나 축소하지 않는다. 현재 채택된 수신/Command 계약과 충돌하는 신규 제안은 착수 시 결정·revision 영향을 기록한다.

| 항목 | 계약 | 근거와 상태 |
| --- | --- | --- |
| AMR ID | 동일 장비의 Topic·등록·상태·명령·이력에 동일 문자열 사용 | 원문 p3~4 |
| 문자열 규칙 | AMR ID는 빈 값·공백·제어문자·/·+·# 금지, 대소문자 구분, 정규화로 다른 ID를 합치지 않음 | 기존 DN-02 개발 계약 |
| CommandId | 요청마다 고유 문자열, 요청 측에서 한 번 생성, 재발행·ACK에서 원래 값 보존 | 원문 p5, 책임 P-01 |
| 업무 매핑 | AMR ID와 cmdId가 모두 원래 요청과 같아야 결과 반영 | 원문의 대상 식별·1:1 연결에서 도출, P-08 |
| Timestamp | 내부 signed Int64 Unix Timestamp, GUI 날짜/시간 변환 | 원문 p3,5 |
| 시간 단위 | Unix milliseconds 명시, 숫자 자릿수로 단위 자동 추측 금지 | 기존 DN-02, 원문 단위 불명 P-10 |
| Timeout 시계 | 관제 수신/발행의 단조 시계로 경과 계산. Payload 시각은 사건 시각으로 별도 보존 | Heartbeat 기존 DN-10, 업무 ACK는 P-08 제안 |
| 화면 시간대 | 표시 시간대·UTC 변환 기준 명시, 원본 숫자 보존 | P-21 결정 필요 |

HeartbeatTimeout, Broker 연결, 운행 State, 명령 업무 상태는 서로 다른 값이다. 어느 하나의 관측만으로 나머지를 임의로 덮어쓰지 않는다.

## 2. MQTT Topic과 방향

| 유형 | Topic | 발행 주체 | 수신 주체 |
| --- | --- | --- | --- |
| Telemetry | amr/{amrId}/telemetry | 가상 AMR | Communication |
| Alarm | amr/{amrId}/alarm | 가상 AMR | Communication |
| Heartbeat | amr/{amrId}/heartbeat | 가상 AMR | Communication |
| Command | amr/{amrId}/command | Communication | 해당 가상 AMR |
| ACK | amr/{amrId}/ack | 가상 AMR | Communication |

원문 p6을 기준으로 한다. 별도 State Topic은 p7/그림에 등장하지만 계약 표에 없으므로 독립 Topic으로 추가하지 않고 Telemetry.state로 처리하는 P-11 정리안을 사용한다. 공통 type 필드는 없고 Command에만 type이 있다.

QoS·중복·Retain·세션·재접속은 운영 확정값이 아니다. 기존 개발 프로파일은 MQTT5/TCP·QoS0·Retain false다. QoS0은 무손실·업무 중복 방지 보장이 아니며, QoS 변경만으로 명령 중복 실행을 해결할 수 없다.

## 3. JSON 공통 검증

기존 DN-02/08 계약은 strict UTF-8 JSON object, 최대65,536bytes·깊이16, 정확한 필드명, 필수 필드 존재, 중복 키·알 수 없는 필드·주석·후행 쉼표 거부다. 신규 연동이 필드를 확장하면 계약 버전과 호환성·재검증을 관리한다.

| 데이터 | 타입과 범위 |
| --- | --- |
| id/cmdId/code | 공백뿐이거나 빈 값이 아닌 string. null 불허 |
| ts | signed Int64 범위의 JSON 정수. 문자열·소수·지수 표기 불허 |
| 좌표 x/y | 유한 숫자. 세계 좌표의 범위·축·도착 오차는 P-07 |
| 속도 sp | 유한 숫자,0이상. 단위 m/s는 기존 개발 계약 |
| 선택 문자열 | 필드가 있으면 null·빈 값·공백뿐인 값 불허 |

이 제한값들은 PDF 확정값이 아닌 기존 개발 결정을 계승한 프로파일이다. 최대 ID/이유/메시지 길이와 세계 좌표 범위는 신규 연동 전에 결정한다. Topic AMR ID와 Payload id가 존재하는 유형은 일치해야 한다.

## 4. 유형별 필드

### Telemetry

| 필드 | 필수 | 계약 |
| --- | --- | --- |
| id | 예 | 발행 AMR ID, Topic과 동일 |
| ts | 예 | 해당 스냅샷 생성 시각 |
| pos | 예 | x/y만 가진 object |
| sp | 예 | 현재 속도, 정지 시0 |
| state | 예 | IDLE/BUSY/STOPPED/FAULT |

원문 p3~4,6. 속도·상태·시각을 pos 안에 넣지 않는다. 발행 주기는 P-07이며 Core 저장은 최신 스냅샷 갱신이다. State 변경만 업무 History로 만든다.

### Alarm

| 필드 | 필수 | 계약 |
| --- | --- | --- |
| id/ts | 예 | 대상 AMR과 사건 시각 |
| code | 예 | Alarm 식별 코드. ALM-{유형}-{일련번호}는 원문 예시이며 범용 강제 정규식이 아님 |
| level | 예 | INFO/WARN/ERR/FATAL |
| alarmState | 예 | ACTIVE/CLEARED는 기존 개발 계약, 원문 전체 enum 미확정 |
| message | 아니오 | 원문 Domain에는 있고 예시에 없어 선택 수신. 상세 화면에 없으면 미상 |

Alarm Payload에는 위치·발생 회차 ID가 없다. Core 내부 회차·History ID와 발생 시 최신 위치 스냅샷을 별도로 관리하는 안은 P-16/19 제안이며 wire 필드를 임의 추가하지 않는다.

### Heartbeat

id/ts/status가 필수다. status=OK는 원문 예시를 사용한 기존 개발 허용값이다. 매 신호의 ts는 발행 시각이며 Timeout 계산의 마지막 정상 수신 시각을 대신하지 않는다. 주기·최초 수신 전 감시·Offline은 별도 설정/정책이다.

### Command

| 필드 | 필수 | 계약 |
| --- | --- | --- |
| cmdId | 예 | 요청 식별자 |
| type | 예 | MOVE/STOP/RESET |
| ts | 예 | 요청 생성 시각 |
| param | 예 | MOVE는 x/y, STOP·RESET은 빈 object |

원문 p5~6. STOP/RESET의 빈 object는 기존 DN-08의 개발 결정이다. 형식 검사와 State/Alarm 수행 가능 판단은 별도다. 정상 형식이더라도 현재 상태가 허용하지 않으면 실제 수행 없이 실패 결과를 보낸다.

### ACK

cmdId/result/ts 필수, id/reason 선택 수신이다. result=SUCCESS/FAILURE는 기존 계약이며 새 가상 AMR의 실패 발행에는 reason을 반드시 넣는다. id가 있으면 Topic ID와 같아야 한다. 원문 p11의 실패 이유 요구를 reason으로 구체화한 DN-08과 P-13을 따른다.

깨진 JSON·없거나 잘못되거나 중복된 cmdId는 임의 ID 응답 없이 Log·폐기한다. 유효하고 유일한 ID가 있는 거부 요청은 같은 ID의 FAILURE를 반환하는 기존 계약을 따른다. 수신 ACK가 유효하다는 사실과 실제 요청에 대응한다는 업무 판정은 별도다.

### 형식 예시

아래5줄은 서로 다른 메시지이며 실행 증거가 아니다. Telemetry·Alarm·성공 결과는 최종 시뮬레이터의 실제 상태에서 생성해야 한다.

```json
{"id":"AMR_001","ts":2208988800123,"pos":{"x":15.2,"y":20.5},"sp":0.8,"state":"BUSY"}
{"id":"AMR_001","ts":2208988800124,"code":"ALM-OBS-001","level":"WARN","alarmState":"ACTIVE","message":"전방 장애물 근접"}
{"id":"AMR_001","ts":2208988800125,"status":"OK"}
{"cmdId":"CMD-example-001","type":"MOVE","ts":2208988800126,"param":{"x":30.0,"y":15.0}}
{"id":"AMR_001","cmdId":"CMD-example-001","ts":2208988800127,"result":"FAILURE","reason":"STATE_NOT_ALLOWED"}
```

STATE_NOT_ALLOWED는 신규 실패 이유 코드의 **예시 제안**이다. STATE_NOT_ALLOWED/ACTIVE_BLOCKING_ALARM/INTERRUPTED_BY_STOP/INTERRUPTED_BY_ALARM 등 코드 목록과 의미는 P-02/13에서 확정한다. 현재 임시 TestClient의 SIMULATION_NOT_IMPLEMENTED는 최종 제품의 정상 기능 결과가 아니다.

## 5. 운행과 명령 결과 정책

| 상태·사건 | 원문 | 명세 적용과 결정 |
| --- | --- | --- |
| IDLE에서 MOVE | ERR/FATAL 없으면 BUSY, 완료 후 IDLE | 원문 기본 동작, 위치 도착 판단 P-07 |
| BUSY에서 새 MOVE | 제한, FIFO/거부는 미정 | 거부 권고 P-20, 단순 MQTT 순서 큐와 구별 |
| BUSY에서 STOP | STOPPED | 원문, 원래 MOVE 결과와 STOP 결과는 P-02 |
| IDLE에서 STOP | p4 가능처럼 서술, p5 BUSY만 | P-03 결정 필요 |
| STOPPED에서 새 명령 | 재출발 경로 미정 | 새 MOVE 허용 확장 권고, 원문 변경 승인 필요 |
| FAULT에서 RESET | 원인 해소 성공 IDLE, 지속 시 FAULT | 원문, Alarm 해제 순서 P-04 |
| ERR/FATAL 발생 | FAULT·MOVE 제한 | 원문, COL 해제 STOPPED와 충돌 P-04 |

완료 ACK 권고안은 MOVE 도착 시 SUCCESS, STOP 실제 정지 시 SUCCESS, RESET 정상 상태 복구 시 SUCCESS다. STOP/장애로 원래 MOVE가 끝나지 못하면 원래 ID의 FAILURE와 중단 이유를 별도로 반환한다. 이 안은 P-02에서 채택할 때 최종 인수 기준으로 고정한다.

## 6. 등록과 Alarm 정책

P-06 권고안은 설정 선등록, 정상 Heartbeat에 한한 신규 등록 허용, 미등록 Telemetry/Alarm/ACK는 Log·업무 반영 제외다. 이는 p7의 Telemetry 자동 등록과 다르므로 채택 전 확정 요구로 쓰지 않는다. Core는 통신 검증을 통과한 메시지에도 별도의 등록 확인을 수행한다.

P-16 권고안은 AMR+code별 활성 Alarm1건, 동일 ACTIVE 반복 시 새 발생 이력 없음, 첫 CLEARED에 해제1회, 해제 후 재발은 새 내부 회차다. 미발생 CLEARED는 상태를 새로 만들지 않고 이상 입력으로 기록하는 안을 권고한다. 새로운 wire 필드는 요구하지 않는다.

## 7. Core 논리 인터페이스

다음은 **신규 설계 제안**이며 제품 내부 메서드나 공개 REST API를 확정하지 않는다. 같은 데이터 의미를 Mock과 실제 구현에 사용한다.

| 작업 | 요청 데이터 | 반환·알림 데이터 | 실패 |
| --- | --- | --- | --- |
| 장비 등록/조회 | AMR ID·기본정보 또는 조회 ID | 등록 여부·기본정보·목록 | 중복 정책·미등록·저장 실패 |
| 최신 상태 조회/갱신 구독 | AMR ID 또는 전체 | ID·X/Y·속도·State·발행시각·수신시각·데이터 유무 | 미등록·미수신·조회 실패 |
| Dashboard 조회 | 집계 범위·필터 | 장비수·State별수·활성Alarm·Command상태별수·스냅샷시각 | 데이터 가용성 실패 |
| 명령 요청 | 대상·type·param | 접수 여부·cmdId·생성시각·거부 사유 | 입력/대상 오류·등록/발행 실패 |
| 명령 조회/결과 알림 | cmdId·대상 확인 | 요청내용·업무상태·결과·이유·요청/수신시각·응답시간 | 미요청·조회 실패 |
| Alarm 목록/상세 | AMR·상태·code·level·내부기록ID | 발생/해제·내용·시각·관측 위치와 출처 | 상세 없음·조회 실패 |
| History 조회 | 시작/종료·AMR·Event·Alarm조건·페이지 | 기록목록·정렬·다음페이지·집계기준 | 기간 오류·저장소 실패 |

GUI 데이터의 '없음'은0값이나 IDLE로 바꾸지 않는다. 화면·Core 스냅샷의 시각을 함께 제공하여 오래된 상태를 최신으로 오인하지 않게 한다. Command ACK만으로 좌표를 갱신하지 않고 실제 Telemetry를 사용한다.

### GUI DataProvider 적용

2026-09-04자 단계별 과제 PDF3페이지가 지정한 과제3 산출물은 Mock 실행 화면과 DataProvider 인터페이스다. 위 논리 계약을 GUI 공급자 경계로 사용한다. Mock 공급자는 GUI 단독 실행 데이터를 제공하고, 실제 공급자는 Core를 경유한다. 두 공급자는 같은 데이터 구조와 요청/결과 의미를 사용하며 화면·ViewModel 변경 없이 실행 구성에서 교체할 수 있어야 한다. 특정 C# 인터페이스명이나 별도 HTTP 서버를 요구하지 않는다.

1차 제공 범위는 장비 목록·위치/상태·MOVE 요청·명령 결과다. Mock 모드는 표시하고 모의 결과를 실제 AMR ACK로 간주하지 않는다. 좌표 변환·UI Dispatcher 처리는 GUI 책임이며 공급자 어댑터는 MQTT/SQLite를 우회 호출하지 않는다. 단독 실행·공급자 교체·실제 시연 완료 기준은 [과제 대응표](assignment_mapping.md)의 D-GUI-01~03에 정의한다. 이 추가 문서 기준은 미실행이며 현재 제품 구현을 인증하지 않는다.

## 8. 명령 업무 상태와 경쟁 조건

P-08 제안: Created(요청 보관), AwaitingAck(발행 후 대기), Succeeded, Failed, PublishFailed, TimedOut를 내부 상태로 구분한다. 이 값은 ACK result enum이 아니다.

| 사건 | 제안 전이와 원칙 |
| --- | --- |
| 요청 접수 | ID·대상·내용을 먼저 보관하여 빠른 ACK도 매핑 가능하게 함 |
| 발행 완료 | 아직 최종 결과가 없다면 AwaitingAck. 이미 온 ACK 결과를 덮어쓰지 않음 |
| 발행 실패 | 최종 ACK가 없는 경우 PublishFailed, 성공 여부 불명 시 구분하여 기록 |
| 정상 ACK | 대상·ID가 일치하면 최초 최종 결과 반영, result/reason 원형 보존 |
| 중복 ACK | 같은 결과 재수신으로 새 최종 이력을 중복 생성하지 않음, 모순 결과는 이상 기록 |
| Deadline 경과 | 미완료 요청은 TimedOut. 실제 실행 실패로 단정하지 않음 |
| 늦은 ACK | 기존 Timeout 판정은 보존하고 늦은 결과를 별도 기록하는 안 권고 |
| 미요청/다른 AMR | 다른 요청의 상태를 변경하지 않고 원인 기록 |

Timeout 시작은 관제의 발행 완료 단조 시각, ACK 경계 판정은 관제 수신 시각 기준을 권고한다. 발행 오류와 빠른 ACK의 경합, 동일 Deadline의 선후 규칙, 요청 재시작 복원·재전송 여부는 P-08에서 최종 확정한다. 응답시간은 관제의 요청 기준 시각에서 ACK 관측까지로 정의해 원격 ts의 시계 오차를 배제한다.

## 9. 업무 이벤트와 저장 모델

| 논리 데이터 | 최소 필드·키 | 근거와 제안 |
| --- | --- | --- |
| 장비 | AMR ID 고유키·기본정보·등록시각 | p16 등록 영속 저장, 세부 필드 제안 |
| 최신 상태 | ID·X/Y·속도·State·발행/수신시각 | p16 최신값. 전체 스냅샷 영속화 여부는 선택 결정 |
| Alarm 발생 회차 | 내부ID·AMR·code·level·상태·발생/해제시각·내용·선택 위치관측 | p16 관리, 회차/위치는 P-16/19 제안 |
| 명령 요청/결과 | cmdId 고유키·AMR·type·param·요청/발행/수신시각·업무상태·result/reason | p16 업무 관리, 영속 범위·복원은 P-08/09 |
| History | eventId·AMR·Event유형·사건/수신시각·관련cmdId/Alarm회차·전후값 | p6~7/16 업무 이력, 키·세부 필드는 도출 |

필수 History 사건은 StateChanged, AlarmActivated, AlarmCleared, CommandRequested, CommandResult다. 명칭은 내부 제안이며 의미는 원문에 근거한다. CommandResult는 실제 ACK 결과와 관제 Timeout/발행 실패를 구분해 저장한다. 매 Telemetry·위치 변경·파싱 오류는 업무 History에 넣지 않는다.

저장 호출의 성공/실패를 Core로 반환한다. 관련 상태 변경과 History 기록의 원자성·실패 시 재시도/보관·조회 일관성·보존 기간은 P-09에서 결정한다. GUI는 SQLite 파일을 직접 읽지 않는다.

## 10. 조회와 화면 계약

P-21 권고: 기간은 시작 포함·종료 제외, 내부 UTC 밀리초로 비교한다. 시간대 변환은 GUI 경계에서 수행한다. 정렬은 사건시각 내림차순+동일시각 내부ID의 안정 순서, 페이지는 그 순서를 유지하도록 정의한다. 실제 방식·페이지 크기·Chart 집계 구간은 결정 대상이다.

GUI-03의 최근20개는 Alarm 발생/해제 이력에서 동일 조건으로 선택하는 안을 권고한다. 현재 활성 Alarm은 별도 Core 목록으로 보여준다. 상세 위치는 발생 당시 마지막 Telemetry와 그 수신시각을 보관한 값이며 실제 장애 위치와 동일하다고 단정하지 않는다. 해당 값이 없으면 미상이다.

Map 좌표계는 세계 좌표 원점·X/Y 양의 방향·단위·범위를 먼저 정하고 화면 원점·축 반전·배율·여백·확대/축소를 연결한다. GUI-02 시험은 원점·양축·모서리·범위 밖 등 기준점의 예상 화면 좌표를 계산해 대조한다. 실제 변환 수치가 미정이면 Map 인수를 완료 처리하지 않는다.

GUI-04는 성공 녹색·실패 적색 외에도 텍스트를 표시하고 대기/Timeout을 구분한다. 응답시간은 Core 계산값을 표시한다. 실제 사용할 State별 Marker 색상과 상태 없는 경우의 색상은 디자인 단계에서 정하되 legend와1:1 대응시킨다.

## 11. 설정과 시험 프로파일

| 설정 | 상태 | 시험 전에 기록할 내용 |
| --- | --- | --- |
| AMR 수·ID·초기 위치/State·경로·속도·장애 | 원문 설정 요구 | 2대 이상 시험 목록, 정상/불량 예제, 시작 실패 정책 |
| Broker·프로토콜·인증·QoS·Retain·KeepAlive | 일부 기존 개발값 | 시험값·버전·세션 설정, 운영과 구분 |
| Heartbeat 주기·Timeout·확인 주기 | 기존 개발값은1초·5초·500ms | 값·허용 지연·최초 수신 전 정책 |
| Telemetry·시뮬레이션 갱신·장애 검사 주기 | P-07 미정 | 단위·주기·관측 횟수·허용 지터 |
| 경로·목표·속도·도착 오차 | P-07 미정 | 목표와 경유점 연결, 오차, 시간 적분/종료 조건 |
| 명령 업무 Timeout | P-08 미정 | 시작/경계, 이동 예상시간과의 관계, 명령별 값 |
| 복구 주기·한도·Backoff | P-05 미정 | 자동 복구 성공 시간, 종료 취소, 재전송 정책 |
| 큐·장비 한도·종료 대기 | 기존256대기/64AMR은 개발값 | 측정 부하·초과 정책·취소 협조·시간 제한 |
| 화면 갱신·전체 지연 | P-21 미정 | 발행→Core→GUI 각 관측시각과 지연 상한 |
| History·조회·DB | P-09/21 미정 | 이벤트 수·정렬/페이지·보관·실패 주입·재시작 |

MOVE 결과 Timeout은 경로 길이/설정 속도에서 예상되는 정상 수행 시간보다 짧게 일괄 설정하지 않는다. 성능 합격은 고정한 프로파일과 실제 측정 결과로 판단한다. 개발용 수치를 운영 확정값으로 표현하지 않는다.

## 12. 검증 증거와 호환성

각 AC는 시험 설정·입력·관측시각·expected/actual·결과·로그 경로·코드 버전·대역 여부와 연결한다. 실제 Broker 시험과 전체 GUI/DB 통합을 구분한다. DB 오류·시간 경계·순서/동시성은 제어 가능한 시계·대역으로도 검증한다.

필드·enum·단위·Topic·상태 의미를 바꾸면 생산자·소비자·Core·GUI·저장소 영향과 마이그레이션·재검증을 기록한다. 새 개발자가 결정 목록을 해석해 임의로 확정하지 않도록 채택한 P 항목과 선택한 프로파일을 버전으로 고정한 후 구현을 시작한다.
