# COM-02 Heartbeat 감시 계약 (S024 / DN-10, 개발용)

기본값은 마지막 정상 수신 후 **5초**, 확인 주기는 **0.5초**다. Timeout은 양수이며 최대1일, 확인 주기는 타이머 해상도를 고려해 최소1ms이고 Timeout 이하여야 한다. 설정으로 바꿀 수 있으며 운영 확정값이 아니다. Broker 연결용 KeepAlive/Timeout과 별개다.

## 무엇을 감시하는가

- 현재 MQTT 연결 세션에서 **첫 정상 Heartbeat를 받은 AMR**부터 감시한다. 아직 한 번도 수신하지 않은 장비는 추정해서 만들지 않는다. 감시 전 장비 목록/등록은 CORE-01 후속이다.
- `MqttSubscriber → AmrMessageDispatcher`의 수신 검사 직전 시각을 잡는다. 유효성 검사에 성공하고 세션의 AMR 수 제한 안에 있는 Heartbeat만 기록한다. Core 전달이나 큐에서 꺼내는 시각을 사용하지 않는다.
- 검증에 실패한 JSON/필드/Topic ID는 시각을 갱신하지 않는다. Telemetry/Alarm/ACK도 Heartbeat 수신 시각을 갱신하지 않는다.
- 정상 Heartbeat는 큐가 가득 차서 Core 전달이 거절되어도 감시 시각을 갱신한다. 이는 생존 신호가 도착했다는 사실이며 메시지의 Core 처리 성공을 뜻하지 않는다.
- AMR별 큐 등록 한도(기본64) 밖의 새 AMR은 감시하지 않고 기존 `AmrLimitReached` 로그를 남긴다. 감시 데이터는 세션 종료 때 폐기한다.

## 시각과 판정

- `TimeProvider.GetTimestamp()`의 단조 시계로 경과 시간을 계산한다. Payload `ts`와 PC 날짜 변경으로 Timeout을 앞당기거나 늦추지 않는다.
- 표시/전달용 수신 시각은 관제의 UTC Unix 밀리초 Int64로 함께 저장한다. 동시 수신의 검증 완료 순서가 달라도 이전 수신 시각으로 되돌리지 않는다.
- `경과 시간 >= Timeout`이면 다음 주기 검사에서 감지한다. 통상 Timeout~Timeout+CheckInterval에 관측되며 OS 스케줄링 지연이 추가될 수 있다. 실제 Broker 검증은 설정 주기 외 500ms의 측정 여유를 명시한다.
- 정상 수신이 끊긴 한 구간에 Timeout 알림은 한 번만 보낸다. 다시 정상 Heartbeat가 들어오면 복구 알림 한 번을 보내고, 이후 중단은 새 Timeout 구간으로 판단한다.

## Core/콘솔 알림

`IHeartbeatEventSink.OnHeartbeatChanged(HeartbeatNotification)`를 별도 경계로 제공한다. 느린 업무 메시지 큐 뒤에 Timeout을 넣지 않는다. 알림 수신자는 짧게 반환해야 하며 콜백에서 연결 종료·재접속 작업을 동기 대기하지 않는다. 알림은 감시 잠금 아래 직렬 호출되며 수신자의 예외는 Trace 오류로 남기고 격리한다.

| Kind | 콘솔 Event | 의미 |
| --- | --- | --- |
| MonitoringStarted | HeartbeatMonitoringStarted | 첫 정상 Heartbeat 수신으로 감시 시작 |
| TimedOut | HeartbeatTimeout | 정상 수신이 설정 시간 이상 없음 |
| Recovered | HeartbeatRecovered | Timeout 이후 정상 수신 재개 |

알림에는 AmrId, LastReceivedUnixMilliseconds, ObservedUnixMilliseconds, ElapsedMilliseconds, TimeoutMilliseconds를 포함한다. 시작/복구 알림의 관측 시각은 해당 정상 수신 시각이고 경과 시간은0이다. Timeout의 관측 시각은 검사 시각이다. 이 계약은 최종 Offline·MqttAlive Ping/Pong·실제 운행 상태를 결정하지 않는다(DN-05 후속).

## 연결 수명주기와 사용

- 정상 종료 및 **감지된 Broker 단절** 때 타이머를 닫고 감시 기록을 버린다. 그 뒤 개별 AMR Timeout을 새로 내지 않는다. 네트워크 문제가 Broker 단절로 감지되기 전에는 Heartbeat Timeout을 먼저 관측할 수 있다.
- 명시적 재접속은 새 감시 객체를 만들어 다시 첫 정상 수신부터 시작한다. 자동 재접속은 이번 범위가 아니다.
- 기존 `MqttConnectionManager(options, processor)`는 감시 없이 송수신한다. 새 `(options, processor, heartbeatOptions, heartbeatSink)` 생성자로 감시를 연결한다. 콘솔의 `--heartbeat`/`--command` 수신 모드는 이 생성자를 사용한다.
- 콘솔 설정: `--heartbeat-timeout-seconds 5 --heartbeat-check-seconds 0.5`. `init.cmd start -Heartbeat`는 기본값을 사용한다. 연결 전용 모드에서는 AMR 감시가 없다.

## 검증 방법

- `tests/Amr.Communication.Tests/HeartbeatMonitorTests.cs`: 제어 가능한 시계로 직전/경계/직후, 시계 변경, 반복 알림, 복구, 불량 입력, 느린 Core/가득 찬 큐, 종료 후 늦은 호출을 검사한다.
- `tools/Amr.Heartbeat.Audit`: 실제 Mosquitto, 시험 AMR2대, 느린 Core/알림 대역으로 정상 수신·A만 중단·불량 입력·복구·단절·명시적 재시작을 검사한다. 실행 상태는 진행 로그 및 evidence를 따른다.
