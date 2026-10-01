# AMR 과제 1 공통데이터 명세서 초안

버전 0.1 · 2026-10-01 · 현재 개발 기준

현재 Communication의 데이터 형식과 검증 기준을 정리한다. 같은 데이터로 송수신과 시험을 할 수 있도록 필요한 항목만 남겼다. State·Alarm Level은 원본 p4 기준이며, 시간 단위·단위계·Alarm 발생/해제 값·Heartbeat 값 등은 기존 개발용 채택값이다. 운영 확정값이나 새 정책을 뜻하지 않는다.

근거: [공통 계약](interface_contract.md), [수신 계약](../../src/Amr.Communication/Validation/CONTRACT.md), [Command 계약](../../src/Amr.Communication/Command/CONTRACT.md), [Heartbeat 계약](../../src/Amr.Communication/Monitoring/CONTRACT.md), [설계 결정 DN-01/02/03/04/08/10](../../feature_list.json).

## 1 공통 데이터

| 항목 | 자료형·단위 | 의미와 확인 조건 |
| --- | --- | --- |
| AMR ID / id | string | 장비 식별자. Topic과 Payload에 같은 값 사용. null·빈 값·공백 문자·제어문자·`/`·`+`·`#` 불허. 대소문자 구분, 자동 변경 없음 |
| cmdId | string | 명령 요청 식별자. 요청 측에서 한 번 생성하고 Command와 ACK에서 보존. null·빈 값·공백뿐인 값 불허. 접두사 형식 강제 없음 |
| ts | JSON 정수 / C# long(Int64), 밀리초 | 메시지 생산자가 기록한 UTC Unix 시각. 문자열·소수·지수 표기 불허. Int64 전체 범위를 그대로 보존하며 초/밀리초 자동 변환 없음 |
| x / y | JSON 숫자 / C# double, m | 현재 위치 또는 이동 목표. 문자열·null·NaN·무한대 불허 |
| sp | JSON 숫자 / C# double, m/s | 현재 속도. 0 이상이며 문자열·null·NaN·무한대 불허 |
| code | string | Alarm 종류를 나타내는 코드. null·빈 값·공백뿐인 값 불허. 예: ALM-OBS-001. 코드 형식 강제 없음 |
| message / reason | 선택 string | Alarm 설명 / ACK 결과 설명. 생략 가능. 있으면 null·빈 값·공백뿐인 값 불허 |

좌표 원점·축 방향·이동 범위·도착 오차는 미정이다. 현재 위치 `pos`와 속도 `sp`, 시각 `ts`는 서로 다른 항목이다.

## 2 MQTT 메시지

Topic은 `amr/{amrId}/{종류}`이며 대소문자를 구분한다. 아래 필수 필드는 모두 있어야 한다. 선택 필드는 생략할 수 있지만, 있으면 동일하게 검사한다.

| 종류와 Topic 끝부분 | 발행 → 수신 | 필수 필드 | 선택 필드 |
| --- | --- | --- | --- |
| Telemetry `/telemetry` | 가상 AMR → Communication | id, ts, pos{x,y}, sp, state | 없음 |
| Alarm `/alarm` | 가상 AMR → Communication | id, ts, code, level, alarmState | message |
| Heartbeat `/heartbeat` | 가상 AMR → Communication | id, ts, status | 없음 |
| Command `/command` | Communication → 대상 가상 AMR | cmdId, type, ts, param | 없음 |
| ACK `/ack` | 가상 AMR → Communication | cmdId, result, ts | id, reason |

| 필드 | 자료형 | 허용값·구조 |
| --- | --- | --- |
| pos | object | 현재 위치 x/y만 포함 |
| state | string | IDLE(대기) / BUSY(동작 수행) / STOPPED(정지) / FAULT(장애) |
| level | string | INFO / WARN / ERR / FATAL |
| alarmState | string | ACTIVE(발생) / CLEARED(해제) |
| status | string | OK |
| type | string | MOVE / STOP / RESET |
| param | object | MOVE는 목표 x/y만 포함. STOP·RESET은 빈 객체 `{}` |
| result | string | SUCCESS(수행 성공) / FAILURE(실패·거부) |

Payload에 id가 있으면 Topic의 AMR ID와 정확히 일치해야 한다. Command에는 id가 없고 ACK는 id를 생략할 수 있으므로 대상은 Topic에서 찾는다. 예를 들어 `amr/AMR_001/ack`의 대상은 AMR_001이다. Heartbeat 수신 여부로 다른 메시지의 대상을 찾지 않는다.

운행 State, Alarm 발생/해제, 통신 상태는 각각 다른 값이다. Alarm 형식의 검증·전달은 지원하지만 실제 발생 조건과 운행 영향은 후속 기능명세에서 정한다.

현재 TestClient는 이동을 구현하지 않아 정상 MOVE·STOP·RESET에도 FAILURE와 SIMULATION_NOT_IMPLEMENTED 이유를 응답한다. Command 발행 성공과 동작 완료는 구분한다.

## 3 입력 검사

- Payload는 UTF-8 JSON 객체다. 주석·후행 쉼표·중복 키·정의되지 않은 필드는 거부한다. 최대 크기는 65,536bytes, 최대 깊이는 16이다.
- 정상 메시지만 Core 전달 경계로 보낸다. 수신 검사 실패 시 Topic·오류 코드·문제 필드·이유를 Log에 기록하고 폐기한다. 이후 정상 메시지는 계속 처리한다.
- 입력 오류 Log에 Payload 원문을 기록하지 않으며, 업무 History와 구분한다. Command 발행 전 검사 실패는 호출자에게 오류를 반환하고 발행하지 않는다.

## 4 관제 내부에서 관리하는 정보

Broker 연결 여부와 개별 AMR의 정상 신호 수신 여부를 확인하기 위한 정보다. MQTT Payload에 추가하지 않는다. 모든 내부 시각은 UTC Unix 밀리초이고, 경과 시간·제한 시간은 밀리초(ms)다. 5초는 5,000ms다.

| 정보 | 필드·자료형 | 필요한 이유·의미 |
| --- | --- | --- |
| Broker 연결 | State: enum | 현재 연결 상태 확인 |
| Broker 연결 | Reason: string | 상태가 변경된 이유 확인 |
| Broker 연결 | TimestampUnixMilliseconds: long | 상태 변경 시각 확인 |
| Broker 연결 | Error: 선택 string | 연결·종료 오류 확인. 오류가 없으면 없음 |
| Heartbeat 감시 | AmrId: string | 어느 AMR의 신호인지 확인 |
| Heartbeat 감시 | Kind: enum | 감시 시작·Timeout·수신 재개 구분 |
| Heartbeat 감시 | LastReceivedUnixMilliseconds: long | 마지막 정상 수신 시각 확인 |
| Heartbeat 감시 | ObservedUnixMilliseconds: long | 감시 시작·Timeout·수신 재개를 확인한 시각 |
| Heartbeat 감시 | ElapsedMilliseconds: double | 마지막 정상 수신 후 지난 시간 확인 |
| Heartbeat 감시 | TimeoutMilliseconds: double | 적용한 수신 대기 제한 시간 확인 |
| 입력 오류 | Topic: 선택 string, Error.Code / Error.Field / Error.Reason: string | 어느 입력의 어떤 부분이 왜 거부됐는지 확인 |

Broker State는 Disconnected(연결 안 됨), Connecting(연결 중), Connected(연결됨), Disconnecting(종료 중), Faulted(접속 실패·예상하지 못한 단절), Disposed(연결 관리 종료)다. 로봇의 운행 State와 구분한다.

Heartbeat Kind는 MonitoringStarted(첫 정상 수신), TimedOut(제한 시간 초과), Recovered(Timeout 이후 정상 수신)다. 시작·수신 재개 시 ElapsedMilliseconds는 0이다.

첫 정상 수신부터 감시하며, **마지막 정상 수신 이후의 경과 시간이 제한 시간 이상이면 검사 시 Timeout을 한 번 알린다.** 다시 정상 신호를 받으면 수신 재개를 한 번 알린다.

Payload의 ts를 사용하지 않고 PC 날짜 변경에 영향을 받지 않는 시계로 경과 시간을 잰다. 불량 Heartbeat와 다른 메시지는 감시 시각을 갱신하지 않는다. 감지된 Broker 단절·종료 시에는 감시를 중단한다. 최종 Offline 판정은 후속 Core 범위다.

필드 기준: [Broker 모델](../../src/Amr.Communication/Mqtt/BrokerConnectionSnapshot.cs), [Heartbeat 모델](../../src/Amr.Communication/Monitoring/HeartbeatNotification.cs), [오류 모델](../../src/Amr.Communication/Validation/IncomingMessageProcessor.cs).

## 5 시험 설정과 확인 기준

현재 개발 기본값이다. 시험에서 변경한 값은 검증 계획·결과에 기록한다.

| 설정 | 기본값 |
| --- | --- |
| Broker Host / Port | 127.0.0.1 / 1883 |
| MQTT ClientId | `amr-control-` + Guid. AMR ID와 별개 |
| MQTT 연결 | MQTT 5 / TCP / QoS 0 / Retain false / 로컬 익명 접속 |
| KeepAlive / 접속·통신 Timeout | 5초 / 3초. 단절 감지 전체가 항상 3초라는 뜻은 아님 |
| Heartbeat Timeout / 확인 주기 | 5초 / 0.5초. 실제 관측 시점에는 검사 주기·실행 지연이 추가될 수 있음 |

| 확인 항목 | 기대 결과 |
| --- | --- |
| 정상 Telemetry·Alarm·Heartbeat·ACK | 해당 모델로 변환해 Core 전달 경계에 전달. ID·시각·값 보존 |
| 정상 Command | MOVE는 x/y, STOP·RESET은 빈 param으로 검증 통과. cmdId 보존 |
| 필수 누락·타입 오류·허용값 오류·ID 불일치·잘못된 JSON | 수신 메시지는 오류 Log 후 폐기, Core 미전달. 다음 정상 입력은 처리 |
| ACK의 선택 id·reason 생략 / 일치하는 id 포함 / 다른 id 포함 | 앞의 두 경우 수신 허용. 다른 id는 거부 |
| ts 문자열·소수·지수 표기 / Int64 경계값 | 앞의 세 형식은 거부. Int64 최솟값·최댓값은 그대로 보존 |
| 첫 정상 Heartbeat → 제한 시간 경과 → 정상 수신 재개 | MonitoringStarted → TimedOut 1회 → Recovered 1회 |
| 잘못된 Heartbeat·다른 메시지 수신 / PC 날짜 변경 | Heartbeat 감시 시각을 갱신하지 않음 / 경과 시간 판정 유지 |
| Broker 접속 성공·예상하지 못한 단절 | Connected·Faulted와 사유·시각 제공. 개별 AMR 운행 상태로 해석하지 않음 |

시험 근거: [수신 데이터 검사](../../tests/Amr.Communication.Tests/MessageValidationTests.cs), [Command·ACK 검사](../../tests/Amr.Communication.Tests/CommandValidationTests.cs), [Heartbeat 감시 검사](../../tests/Amr.Communication.Tests/HeartbeatMonitorTests.cs), [연결 상태 검사](../../tests/Amr.Communication.Tests/ConnectionLifecycleTests.cs).

이 표는 공통데이터 확인 항목이며 과제 1 전체 완료 기준을 대신하지 않는다. 실제 MQTT 송수신은 실제 Broker에서 별도 검증한다. 명령 업무 Timeout·중복/늦은 ACK·Alarm 업무 규칙·Core/GUI/DB 데이터는 후속 기능명세에서 정한다.
