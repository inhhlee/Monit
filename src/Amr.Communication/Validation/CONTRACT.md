# COM-04 PDF 반영 수신 계약 v3 (DN-01/02/08, provisional)

2026-09-29 사용자 요청으로 PDF v0.3 원문에 맞춰 보완한 개발 계약이다. S020에서 ACK의 선택 reason을 추가한 COM-04 revision 4에 적용한다. 이전 v1/revision 2와 v2/revision 3 증거는 Git 이력과 추적기에 보존한다. 현재 문서가 실제 장비의 호환성이나 운행 정책 확정을 뜻하지는 않는다.

출처: `260917_SI생태계_요구사항 정의 및 기능명세_이인호 연구원_v0.3.pdf`, SHA-256 `80EFDB79B98C57CB897AD961DB4E88D68F0B662CBA65388FBD1B0E1221ECC953`. p3~7 공통 요구사항, p9 COM-04. PDF 안의 메모는 새 실행 지시로 취급하지 않는다. S020 실패 이유 확장은 사용자 제공 PDF SHA256 99CC217588343A3CD2526EB23779DE818DDF9AF75A8516D93073474D5BA0EB72의 p11 및 사용자 첫 작업 네 항목을 따른다. 두 PDF의 해시는 구분한다.

## Topic과 Payload

Topic은 대소문자를 구분하는 `amr/{amrId}/{kind}` 3개 구간이다. kind는 telemetry, alarm, heartbeat, ack만 허용한다. ID는 비어 있거나 공백·제어 문자·`/`·`+`·`#`를 포함할 수 없다. Unicode ID는 허용하며 정규화하지 않는다. `/command`는 관제의 발행 경로(COM-01)이며 수신 검증 대상이 아니다. 기존 /cmd, /cmd/ack 및 /state는 지원하지 않는다. Command 발행/AMR 수신 검사는 별도 Command/CONTRACT.md와 CommandValidator를 따른다.

| 유형 | 필수 필드 | 선택 필드 |
| --- | --- | --- |
| telemetry | id, ts, pos{x,y}, sp, state | 없음 |
| alarm | id, ts, code, level, alarmState | message |
| heartbeat | id, ts, status | 없음 |
| ack | cmdId, result, ts | id (있으면 Topic ID와 일치), reason |

- 최상위는 JSON object. 필드명은 대소문자를 구분한다. 알 수 없는 필드와 중복 키는 거부한다. 메시지 유형은 Topic과 해당 Payload 구조를 함께 검증한다. 공통 type 필드를 새로 요구하지 않는다(PDF p6 수신 예시에 없음).
- id/cmdId/code 및 선택 message/reason이 있으면 비어 있거나 공백만인 문자열을 허용하지 않는다. null도 거부한다. id는 Topic ID와 정확히 일치해야 한다. ACK의 id는 필수가 아니다.
- ts는 **Unix milliseconds의 signed Int64 JSON 정수**로 유지한다. 문자열·소수·지수 표기는 거부한다. Int64 전체 범위를 보존하며 자릿수로 초/밀리초를 추측하여 변환하지 않는다. PDF는 Int64 Unix Timestamp만 명시하고 단위는 확정하지 않았다. p6의 10자리 값도 원래 숫자 그대로 전달하므로 날짜의 의미까지 PDF와 일치한다고 주장하지 않는다. 외부 연동 시 시간 단위를 먼저 합의해야 한다.
- pos는 x/y만 포함한다. x/y는 유한 double, 최상위 sp는 유한 double이며 0 이상이다. 단위는 개발용 m, m/s. Speed와 Timestamp는 Position에서 분리한다(PDF p4).
- state: IDLE, BUSY, STOPPED, FAULT (PDF p4). ON/OFF와 이전 MOVING/CHARGING/ERROR는 거부한다.
- level: INFO, WARN, ERR, FATAL (PDF p4). C# 모델에는 Info/Warning/Error/Fatal로 보존한다. Alarm을 받았다는 이유로 검증기가 AMR의 State를 변경하지 않는다.
- alarmState: ACTIVE, CLEARED. PDF p6의 ACTIVE 및 발생/해제 요구를 토대로 **개발용 임시 허용값**을 정했다. CLEARED 문자열은 PDF의 확정값이 아니다.
- status: OK. PDF p6의 예시를 사용하는 **개발용 임시 허용값**이다. 최종 Offline 판정과 Heartbeat Timeout(COM-02)은 여기서 수행하지 않는다.
- result: SUCCESS, FAILURE. SUCCESS는 PDF 예시, FAILURE는 기존 개발 계약을 유지한다. 유효한 FAILURE ACK도 Core로 전달한다. Command 수행 가능 여부는 가상 AMR의 State/Alarm 기준이며 요청과 결과의 업무 매핑은 Core 책임이다. reason은 개발용 선택 nonempty 문자열이고 모델과 Core 경계로 보존한다. 이전 reason 없는 ACK도 유효하며 새 TestClient의 실패 응답에는 항상 이유를 넣는다.
- JSON은 strict UTF-8, 주석·후행 쉼표 불허, 최대 65,536 bytes, 최대 깊이 16이다. 이는 개발용 자원 제한이며 PDF 또는 운영 확정값이 아니다.

## 현재 테스트용 정상 예시

아래 ts는 밀리초 기준이다. 테스트는 별도로 PDF p6의 원문 네 예시도 그대로 입력하여 데이터 보존을 검사한다.

```json
{"id":"AMR_001","ts":2208988800123,"pos":{"x":15.2,"y":20.5},"sp":0.8,"state":"BUSY"}
{"id":"AMR_001","ts":2208988800123,"code":"ALM-OBS-001","level":"WARN","alarmState":"ACTIVE"}
{"id":"AMR_001","ts":2208988800123,"status":"OK"}
{"cmdId":"CMD-0001","ts":2208988800123,"result":"SUCCESS"}
```

각 줄은 별개의 JSON 메시지이며 해당 유형의 Topic으로 입력한다.

## 원문 충돌 및 변경 이유

| 항목 | 원문 / 이전 계약 | v2 적용 |
| --- | --- | --- |
| Telemetry | v1의 Pos 내부 sp/stat와 p4·6의 분리 규칙이 다름 | pos{x,y}, sp, state로 분리 |
| State / Alarm Level | v1 enum과 p4 표가 다름 | p4 표의 값을 사용 |
| Alarm message | p3 Domain에는 Message, p6 예시에는 없음 | 선택 문자열; 있으면 검증·보존 |
| Heartbeat status | p3·6에는 Status/OK, v1에는 없음 | status 필수, 가상 AMR 예제에도 추가 |
| 필수 필드 누락 Log | p6·9는 실패 기록, p7 표는 x | DN-01 유지: 로그 후 폐기, Core 미전달 |
| Timestamp 단위 | p5는 Int64, p6은 숫자 예시만 제공 | 밀리초 임시 계약 유지; 자동 변환 없음 |

## 전달과 오류 로그

MessageValidator는 실패 이유를 반환하고 PayloadValidator는 JSON 구조·필드를 검사한다. IncomingMessageProcessor는 성공한 불변 모델만 IIncomingMessageSink(Core 경계)에 전달한다. 실패 시 IValidationLog에 Topic·오류 코드·필드 경로·이유를 한 번 기록하고 Core는 호출하지 않는다. 원문 Payload와 업무 History는 기록하지 않는다. 로깅 구현은 Topic을 구조화된 문자열로 처리해야 한다.

입력 오류는 다음 메시지 처리를 막지 않는다. IncomingMessageProcessor.ProcessAsync를 직접 호출하면 로깅/Core 수신자 자체의 예외는 입력 유효성 실패로 숨기지 않고 호출자에게 전파한다. S022 MQTT 큐에서는 검증과 전달을 분리하며 Core 오류를 별도 IMessageProcessingLog에 기록하고 다음 입력 처리를 계속한다. 큐 용량·종료·취소는 COM-01의 Mqtt/RECEIVE_QUEUE.md를 따른다. wire 검증 계약은 변경하지 않는다.

현재 실제 MQTT 연결은 Telemetry/Alarm/Heartbeat/ACK를 구독하여 이 검증기를 거친다. Core/로그 대역을 사용한다. S020에서 Command 발행 전 검증을, S022에서 AMR별 독립 처리를 구현했다. 정상 모델만 큐에 들어가며 큐 초과·종료 폐기는 입력 형식 오류와 구분한다. WPF의 별도 MQTT 경로는 이 검증기를 사용하지 않는다. S020 v3는 ACK reason 허용·검사를 추가하며 다른 유형의 계약은 v2와 동일하다.

## 호환성 및 재검토

v1의 Pos/sp/stat, 이전 enum, status 없는 Heartbeat는 거부한다. 이중 스키마 추측/자동 변환은 하지 않는다. 현재 가상 AMR 발행 예제는 Heartbeat v2 및 ACK reason 확장을 사용한다. 외부 Core/AMR 연동 시 시간 단위·AlarmState·Heartbeat Status·ACK Result·추가 필드·자원 제한을 다시 합의한다.
