# AMR별 수신 처리 (S022 / DN-09)

처음 읽는다면 [실제 수신 흐름도와 코드 읽는 순서](RECEIVE_FLOW.md)를 먼저 참고한다.

개발용 계약이다. MQTT 수신부에서 COM-04 검사를 완료한 불변 메시지만 AMR별 Channel에 넣는다. 같은 연결 안에서 같은 AMR의 네 유형은 하나의 큐를 공유하고 소비자 하나가 도착 순서대로 Core 호출을 await한다. 다른 AMR은 별도 비동기 작업으로 처리한다. 전용 스레드를 AMR마다 만들지 않는다.

- 기본 제한: AMR당 대기 메시지 256개(처리 중 1개 별도), 연결 세션당 AMR 64개. 테스트는 작은 값을 주입할 수 있다. 세션 종료 때까지 큐를 유지하고 재접속은 새 큐를 사용한다.
- 수신부는 가득 찬 큐를 기다리지 않는다. 새 메시지를 거부하고 QueueFull 또는 AmrLimitReached 처리 로그를 남긴다. 정상 입력의 자원 초과이므로 JSON 검증 실패와 구분한다. QoS0 개발 경로이며 자동 재전송·유실 없는 업무 처리는 보장하지 않는다. 운영 큐 크기와 과부하 정책은 후속 검토다.
- Core 오류는 ProcessingFailure/HandlerFailed로 기록하고 다음 메시지를 계속 처리한다. 원문 Payload·예외 Message는 로그에 넣지 않는다. 기존 IncomingMessageProcessor.ProcessAsync 직접 호출의 예외 전파 계약은 유지한다.
- 종료·연결 끊김 시 새 수신을 막고 실행 중 Core 호출의 CancellationToken을 취소한다. 남은 큐는 폐기하고 개수를 처리 로그에 기록한다. 정상 DisconnectAsync는 작업 종료를 기다리되 기존 연결 timeout을 적용한다. Core는 취소 토큰에 협조해야 하며 서로 다른 AMR의 동시 호출을 지원해야 한다.
- 취소를 무시하는 Core 작업을 강제 중단하지 않는다. 종료 대기가 timeout되면 연결 정리 오류로 보고하며 백그라운드에서 해당 작업의 종료를 관찰한다. 취소 후 다음 메시지는 전달하지 않는다. Core 처리 내부에서 자기 연결의 종료를 await하지 말고 소유자에게 종료를 요청한다.
- 검증 오류는 IValidationLog, 큐 초과·Core 오류·중단은 IMessageProcessingLog에 기록한다. 별도 처리 로그를 주지 않은 라이브러리 호출자는 Trace 오류 출력을 사용한다. 처리 로그 구현은 빠르게 반환해야 한다.

Heartbeat Timeout은 여기서 계산하지 않는다. COM-02에서 마지막 정상 MQTT 수신 시각과 Core 처리 시각을 구분해야 한다. 현재 큐 정책은 최종 Offline 판단·Command/ACK 업무 매핑·실제 운행 성공을 의미하지 않는다.
