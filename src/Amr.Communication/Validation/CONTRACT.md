# COM-04 내부 수신 계약 v1 (DN-01/02, provisional)

2026-09-21 임시 채택. PDF 원본이 없는 상태에서 구현·검증할 내부 테스트 계약이며 실제 장비 호환 계약이 아니다. COM-04 revision 2에 적용한다. COM-01/02/05는 착수 시 이 계약을 재검토하고 해당 revision을 갱신한다. 기존 WPF와 TelemetryPayload의 역직렬화 경로는 이 검증기를 사용하지 않는다.

## Topic과 Payload

Topic은 대소문자를 구분하는 `amr/{amrId}/{kind}` 3개 구간이다. kind는 telemetry, alarm, heartbeat, ack만 허용한다. ID는 비어 있거나 공백·제어 문자·`/`·`+`·`#`를 포함할 수 없다. Unicode ID는 허용하며 정규화하지 않는다. 기존 MqttTopics의 cmd/ack와 state는 이 수신 계약에 해당하지 않으며 COM-01에서 정리한다.

| 유형 | 필수 필드 | 추가 허용 필드 |
| --- | --- | --- |
| telemetry | id, ts, Pos (x, y, sp, stat 모두 필수) | 없음 |
| alarm | id, ts, code, level, message | 없음 |
| heartbeat | id, ts | 없음 |
| ack | cmdId, result, ts | id (있으면 Topic ID와 일치) |

- 최상위는 JSON object. 필드명은 대소문자를 구분한다. 알 수 없는 필드와 중복 키는 거부한다. type 필드는 요구하지 않으며 메시지 유형은 Topic에서 식별한다.
- id/cmdId/code/message는 비어 있거나 공백만인 문자열을 허용하지 않는다. id는 Topic ID와 정확히 일치해야 한다. ACK의 id는 필수가 아니다.
- ts는 **Unix milliseconds의 signed Int64 JSON 정수**이다. 문자열·소수·지수 표기는 거부한다. Int64 전체 범위를 보존하며 표시용 날짜로 변환하거나 현재 수신 시각으로 바꾸지 않는다. 미래·과거 시간의 업무 정책과 Heartbeat 수신 시각 관리는 후속 범위다.
- Pos는 기존 내부 예시를 출발점으로 사용한다. x/y는 유한 double, sp는 유한 double이며 0 이상이다. 좌표·속도 단위는 내부 실험에서 m, m/s이다.
- stat는 IDLE, MOVING, CHARGING, ERROR 중 하나다. level은 INFO, WARNING, ERROR 중 하나다. result는 SUCCESS, FAILURE 중 하나다. 모두 대소문자를 구분한다. 유효한 FAILURE ACK도 전달한다.
- JSON은 strict UTF-8, 주석·후행 쉼표 불허, 최대 65,536 bytes, 최대 깊이 16이다. 이는 개발용 자원 제한이며 운영값이 아니다.

## 전달과 오류 로그

MessageValidator는 실패 이유를 반환하며 PayloadValidator는 JSON 구조·필드를 검사한다. IncomingMessageProcessor는 성공한 불변 모델만 IIncomingMessageSink(Core 경계)에 전달한다. 실패 시 IValidationLog에 Topic·오류 코드·필드 경로·이유를 한 번 기록하고 반환하며 Core는 호출하지 않는다. 원문 Payload는 로그에 넣지 않는다. 업무 History를 생성하지 않는다. 로깅 구현은 Topic을 구조화된 문자열로 처리해야 한다.

입력 오류는 다음 메시지 처리를 막지 않는다. 로깅/Core 수신자의 예외는 입력 유효성 실패로 숨기지 않고 호출자에게 전파한다. 이 컴포넌트는 MQTT 구독·큐·재시도·Heartbeat 시계·Core 업무 상태를 구현하지 않는다. COM-01에서 실제 수신 바이트가 반드시 이 경로를 거치도록 연결하고 검증해야 한다.

운영 연동, PDF 원본 제공, 실제 Core/AMR 요구 확인 시 필드·enum·시간 단위·추가 필드 정책·크기 제한을 재검토한다.
