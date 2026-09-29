# S022 AMR별 비동기 수신 처리

AMR별 Channel 대기열과 단일 비동기 소비자를 구현했다. MQTT 수신부는 검사를 통과한 모델을 큐에 넣고 다음 수신을 계속한다. 같은 AMR은 처리 완료를 기다리며 순서를 지키고 다른 AMR은 자기 작업으로 진행한다.

## 실제 비교

| 실험 | S021 변경 전 | S022 변경 후 |
| --- | --- | --- |
| A 처리 5초 보류 중 B 처리 | 실패: A 시작 후 약5.011초, A 해제 뒤 처리 | 통과: A 시작 후 약0.041초, A는 여전히 보류 중 |
| 같은 A의 세 메시지 | 순서 유지 | 401 → 402 → 404, 추월 없음 |
| 잘못된 입력 후 회복 | 통과 | 오류4건 로그·폐기 후 정상 수신 |

S022 같은 시계 기록: A 시작862.399ms, B 완료903.555ms, A 해제5865.4416ms. 이는 한 실험의 관측값이며 지연 보장값이 아니다. Payload ts는 시험 식별값이고 시간 측정은 Stopwatch를 사용한다. 실제 Mosquitto 및 고유 ID의 테스트 발행자·Core/로그 대역으로 검증했으며 실제 장비/GUI/이동 검증이 아니다.

## 추가로 확인한 동작

- AMR당 큐가 가득 차면 새 입력만 QueueFull 처리 로그로 거부하고 다른 AMR은 계속 처리한다. 개발 기본값은 대기256개/세션64AMR이며 단위 테스트는 경계를 확인하기 위해 용량1을 사용했다.
- 잘못된 입력은 큐 자원을 차지하지 않는다. 연결의 AMR 큐 개수가 제한에 도달하면 AmrLimitReached를 기록하며 기존 AMR은 계속 처리한다.
- Core 처리 예외는 HandlerFailed로 기록하며 같은 AMR의 다음 메시지를 처리한다. 별도 처리 로그 구현의 예외도 작업을 중단하지 않는다.
- 종료 시 active Core 호출을 취소하고 pending 메시지를 폐기·기록한다. 늦은 입력 거부, 반복 종료, 종료 대기 취소도 검증했다.
- 실제 Broker에서 처리 중 종료, 명시적 재연결 후 새 큐 수신, Broker가 연결을 끊은 경우의 작업 취소까지 통과했다. 마지막 시나리오는 시험용 ClientId의 중복 접속으로 해당 테스트 연결만 끊었으며 사용자 연결은 변경하지 않았다.
- 기존 명령·ACK 왕복9항목, Broker 연결 감시5항목도 통과했다. 정상 MOVE의 FAILURE/SIMULATION_NOT_IMPLEMENTED 의미는 그대로다.

## 증거

- `development/queues.trx`, `development/test.log`: 새 큐 단위 테스트9건 통과.
- `../../bootstrap/20260929T142934255+0900-6b3c68/`: 시작 기준선412건.
- `../../bootstrap/20260929T143500963+0900-7abef9/`: 전체 빌드·제품384 + 도구37 = **421건 통과**, skip0, 기존 NU1701 경고6건.
- `audit-build.log`: 진단 도구 빌드 통과.
- `broker-probe/result.json`, `broker-probe/processing-timeline.json`, `broker-probe/processing-failures.json`, `broker-probe/typed-messages.json`, `broker-probe.log`: **실제 진단12/12 통과**. 진단 종료0은 수집 완료 의미이므로 개별 결과도 확인했다.
- `broker-20260929T143602/result.json`과 로그: 현재 Console/TestClient의 명령·Heartbeat 회귀 **9/9 통과**.
- `../../COM-03/20260929T143549-3d725957a3d84feba14ae999e660cf38/result.json`: COM-03 실제 **5/5 통과**. 종료 감지0.067초, 응답 차단3.056초, idle6.002초.
- `source-sha256.json`: 실행 소스와 기존 사용자 변경의 해시. 시작 HEAD dc681f7, 사용자 init.ps1/solution/주석 변경이 있는 환경이다. 진단 소스의 주석만 실행 직전 한국어로 정정했으며 실행 동작은 빌드 로그 시점과 동일하다.

## 재검증과 제한

Broker를 켜고 `init.cmd verify`로 빌드한 다음 다음을 실행한다. 증거 경로는 매번 새로 만든다.

```powershell
dotnet build tools/Amr.Communication.Audit --no-restore
$runPath = 'evidence/COM-01/queue-check-' + (Get-Date -Format yyyyMMddTHHmmss)
dotnet tools/Amr.Communication.Audit/bin/Debug/net10.0/Amr.Communication.Audit.dll 127.0.0.1 1883 $runPath 5000
```

Core는 AMR 간 동시 호출과 CancellationToken에 협조해야 한다. 취소를 무시하는 Core는 강제 종료하지 않으며 기존 timeout까지 기다린 뒤 연결을 정리하고 작업 종료는 계속 관찰한다. 큐 초과·종료 때는 데이터가 폐기되므로 현재 QoS0 실습을 유실 없는 업무 처리로 확대하지 않는다. 자동 재접속, Heartbeat Timeout, Command/ACK 전체 업무 매핑, 실제 이동은 후속이다.

COM-01 revision4의 검증10항목은 모두 현재 증거를 기록했다. 전체 상태는 기존 사용자 요청에 따라 in_progress를 유지했다. COM-03/04 passing과 COM-02/05 not_started는 유지한다. 다음은 COM-01 전체 완료 판정 정리 후 COM-02 Heartbeat Timeout이다.
