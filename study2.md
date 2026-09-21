# AMR 공부 2 — COM-03 연결 관리와 COM-01 송수신 준비

작성 기준: 2026-09-22, S010 제품 코드와 현재 기능 추적기. 기초 용어·전체 파일 지도·COM-04 검증 흐름은 [Studyreadme.md](Studyreadme.md)를 먼저 참고한다.

**이 문서의 목표는 “연결된 상태”와 “메시지를 올바르게 처리할 수 있는 상태”를 구분하고, 두 기능의 코드와 테스트를 설명할 수 있게 되는 것이다.**

| 기능 | 현재 상태 | 이 문서에서 공부할 것 |
| --- | --- | --- |
| COM-03 | passing, revision 2 | 실제로 구현한 연결·상태·취소·자원 해제 코드 |
| COM-04 | passing, revision 2 | COM-01이 반드시 거쳐야 하는 기존 수신 검증 경계 |
| COM-01 | not_started, revision 1 | 앞으로 만들 구독·발행·AMR별 큐의 개념과 검증 설계 |

COM-01의 흐름도·큐 구성·추가 테스트는 **학습용 설계 제안**이다. 이번 문서 작성으로 구현·정책을 확정하거나 기능 상태를 변경하지 않았다. 최종 기준은 [feature_list.json](feature_list.json)의 verification과 design_notes다.

## 1. 꼭 기억할 세 가지

1. **Broker에 연결됐다고 구독까지 완료된 것은 아니다.** COM-03은 연결 수명주기를 담당하고, COM-01은 구독·발행과 메시지 처리 경로를 담당한다.
2. **비동기라고 순서가 자동으로 보장되지는 않는다.** 같은 AMR의 처리는 앞선 작업이 끝난 뒤 다음 작업을 시작해야 한다.
3. **통신 성공과 로봇의 명령 수행 성공은 별개다.** 발행 결과, MQTT 프로토콜 응답, `amr/{id}/ack`의 업무 응답을 구분해야 한다.

처음에는 COM-03의 `ConnectAsync`와 `ConcurrentConnectsDoNotDuplicateSessions`를 함께 읽는다. 그다음 COM-01의 같은 AMR 순서·다른 AMR 독립성 예제를 읽으면 된다.

## 2. COM-03 — 파일을 이 순서로 읽기

| 순서 | 파일 | 읽으며 답할 질문 |
| --- | --- | --- |
| 1 | [BrokerConnectionOptions.cs](src/Amr.Communication/Mqtt/BrokerConnectionOptions.cs) | 어디에 연결하고 얼마나 기다리는가? |
| 2 | [BrokerConnectionSnapshot.cs](src/Amr.Communication/Mqtt/BrokerConnectionSnapshot.cs) | 상태와 이유를 어떤 데이터로 알리는가? |
| 3 | [IBrokerClient.cs](src/Amr.Communication/Mqtt/IBrokerClient.cs) | 실제 통신 수단이 지켜야 할 약속은 무엇인가? |
| 4 | [MqttClientService.cs](src/Amr.Communication/Mqtt/MqttClientService.cs) | MQTTnet을 실제로 호출하는 곳은 어디인가? |
| 5 | [MqttConnectionManager.cs](src/Amr.Communication/Mqtt/MqttConnectionManager.cs) | 요청 순서·실패·취소·이전 연결을 어떻게 관리하는가? |
| 6 | [Console/Program.cs](src/Amr.Communication.Console/Program.cs) | 설정을 읽고 연결을 시작·종료하는 입구는 어디인가? |
| 7 | [ConnectionLifecycleTests.cs](tests/Amr.Communication.Tests/ConnectionLifecycleTests.cs) | 위 규칙이 깨지는 상황을 어떻게 재현하는가? |

### 역할을 나눈 이유

```text
Program: 사용자 설정과 실행·종료
  → Manager: 연결 상태·순서·수명 관리
    → IBrokerClient: 연결 수단에 대한 인터페이스
      → 실제 실행에서는 MqttClientService → MQTTnet → Broker
      → 단위 테스트에서는 FakeClient
```

Manager가 MQTTnet에만 직접 의존하면 “연결 도중 취소”, “실패한 옛 연결에서 늦게 끊김 발생” 같은 상황을 쉽게 만들기 어렵다. 인터페이스 뒤에 FakeClient를 넣으면 네트워크 없이도 그 상황을 제어할 수 있다.

### Func와 람다를 여기서 익히기

Manager에는 다음 필드가 있다.

```csharp
private readonly Func<IBrokerClient> _createClient;
```

`Func<IBrokerClient>`는 **호출하면 IBrokerClient를 돌려주는 함수**라는 뜻이다. 클라이언트 객체 자체를 보관하는 `_client`와 다르다.

- 실제 생성자는 `() => new MqttClientService()`를 사용한다.
- 테스트 생성자는 `() => client`나 `() => clients.Dequeue()`를 받는다.
- `_createClient()`를 호출하는 시점에 그 함수가 실행된다.
- `clients.Dequeue()`를 사용하면 첫 시도에는 실패하는 대역, 두 번째에는 성공하는 대역을 줄 수 있다.

이것이 “객체 생성 방법을 바깥에서 전달한다”는 의미다. 별도의 의존성 주입 프레임워크가 있어야 가능한 방식은 아니다.

## 3. 연결 시작부터 성공까지 한 번 따라가기

대상은 [MqttConnectionManager.cs](src/Amr.Communication/Mqtt/MqttConnectionManager.cs)의 `ConnectAsync`다.

| 순서 | 실제 동작 | 필요한 이유 |
| --- | --- | --- |
| 1 | `_operation.WaitAsync`로 진입 순서를 기다림 | 연결·해제 요청이 겹치지 않게 함 |
| 2 | 이미 해제된 Manager인지 검사 | 수명이 끝난 객체의 재사용 방지 |
| 3 | 현재 클라이언트와 Snapshot이 모두 연결 상태면 true 반환 | 연결 중복 생성 방지 |
| 4 | 이전 클라이언트를 정리하고 Connecting 기록 | 새 연결 시도를 구분 |
| 5 | 새 클라이언트 생성, 끊김 이벤트 등록 | 실제 연결 대상과 이벤트 발생 객체를 연결 |
| 6 | 호출자 취소와 제한 시간을 적용할 토큰 구성 | 무기한 기다리지 않고 취소 요청 전달 |
| 7 | 클라이언트의 ConnectAsync를 await | 실제 연결 완료를 기다림 |
| 8 | await 이후 연결 상태를 다시 검사 | 연결 완료 직전 끊기는 경쟁 상황을 처리 |
| 9 | 문제가 없으면 Connected 기록 후 true 반환 | 확인된 성공만 외부에 알림 |
| 10 | finally에서 `_operation.Release()` | 실패하더라도 다음 요청이 진입할 수 있게 함 |

**8번을 꼭 공부한다.** 네트워크에서는 “연결 함수가 완료됨”과 “그 직후에도 연결이 살아 있음” 사이에 이벤트가 끼어들 수 있다. 코드가 await 다음에 상태를 다시 확인하는 이유다.

이를 확인하는 기존 테스트는 `DisconnectBeforeConnectReturnsCannotPublishConnectedState`다. FakeClient가 연결 메서드를 반환하기 전에 끊김을 발생시키고, 테스트는 Connected 알림이 나가지 않았는지 검사한다.

### 실제 MQTTnet 호출은 어디에 있나

[MqttClientService.cs](src/Amr.Communication/Mqtt/MqttClientService.cs)의 `ConnectAsync`에서 옵션 Builder에 주소·포트·ClientId·MQTT 5·KeepAlive·Timeout을 넣는다. 실제 라이브러리 호출 결과가 Success인지 확인한 뒤 Manager로 돌아온다.

`.WithCleanStart()`는 기존 세션을 이어받는 대신 새 세션으로 시작하도록 요청하는 설정이다. 이것만으로 재접속·재구독이 구현되는 것은 아니다. 현재 프로젝트의 자동 재접속·재구독은 후속 범위다. 프로토콜의 세션 의미는 [MQTT 5 표준, 3.1.2.4](https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html)를 참고한다.

## 4. 상태·실패·취소를 구분하기

| 상황 | 현재 코드의 대표 결과 | 대표 Reason |
| --- | --- | --- |
| 연결 요청 시작 | Connecting | ConnectRequested |
| 연결 성공 | Connected | Connected |
| 최초 접속 실패·내부 연결 Timeout | Faulted, ConnectAsync는 false | ConnectFailed |
| 실제 연결 시도 중 호출자 취소 | Disconnected, ConnectAsync는 false | ConnectCancelled |
| 연결 중·연결 후 예상하지 못한 단절 | Faulted | ConnectionLost |
| 사용자의 정상 해제 완료 | Disconnected | DisconnectCompleted |
| 해제 중 예외가 나도 정리 완료 | Disconnected | DisconnectCleanup |
| Manager 수명 종료 | Disposed | Disposed |

이 표는 모든 호출의 결과를 하나로 단순화한 표가 아니다. 특히 `_operation` 진입을 기다리다 취소되면 현재 코드는 false만 반환하고 Snapshot을 새로 바꾸지 않는다. 이미 Dispose된 Manager에 ConnectAsync를 호출하면 `ObjectDisposedException`이 발생한다.

### 취소 토큰은 “강제 종료 버튼”이 아니다

현재 코드의 핵심이다.

```csharp
using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
timeout.CancelAfter(_options.Timeout);
await client.ConnectAsync(_options, timeout.Token).ConfigureAwait(false);
```

호출자가 취소하거나 제한 시간이 지나면 전달된 토큰이 취소 상태가 된다. 작업을 실제로 멈추려면 호출받은 통신 구현이 그 토큰을 따라야 한다. 테스트의 FakeClient는 `Task.Delay(Timeout.Infinite, token)`을 사용해 취소 가능한 장기 대기를 만든다.

반환값 `false`만 보면 취소와 접속 실패를 구분할 수 없으므로 Snapshot의 State와 Reason도 같이 읽는다.

### async/await에서 꼭 알아둘 것

`await`는 기다리는 동안 스레드를 계속 점유하는 방식의 대기를 피하고, 작업 완료 후 이어서 실행하게 한다. 하지만 다음 줄이 자동으로 동시에 실행되는 것은 아니다. [Microsoft의 C# 비동기 프로그래밍 설명](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/)도 순차 await와 여러 작업 시작을 구분한다.

이 코드에서는 세마포어가 연결 작업의 순서를 정한다. `ConfigureAwait(false)`는 기존 컨텍스트로 복귀할 필요가 없다는 뜻이며 잠금이나 병렬 실행을 대신하지 않는다. UI 갱신은 나중에 GUI 계층에서 별도로 처리해야 한다.

## 5. 가장 중요한 동시성 사례 두 가지

### 사례 A: 연결 요청이 두 번 겹칠 때

```text
요청 A: 세마포어 진입 → 실제 연결을 시작하고 대기
요청 B: 세마포어 앞에서 대기
요청 A: 연결 성공 → Connected → 세마포어 반환
요청 B: 진입 → 이미 연결된 상태 확인 → 새 연결 없이 true
```

대응 테스트: `ConcurrentConnectsDoNotDuplicateSessions`.

핵심 Assert는 단순히 두 요청 모두 true인지뿐 아니라 **클라이언트를 만든 횟수가 1인지** 확인하는 것이다. 성공만 검사하면 중복 연결을 만들어 놓고도 테스트가 통과할 수 있다.

### 사례 B: 예전 연결에서 늦은 끊김 알림이 올 때

```text
클라이언트 A: 첫 연결 실패 → 정리됨
클라이언트 B: 재시도 성공 → 현재 연결
클라이언트 A: 뒤늦은 Disconnected 이벤트 발생
Manager: 이벤트 발생 객체 A와 현재 객체 B를 비교 → 오래된 이벤트 무시
```

대응 테스트: `FailedConnectionAndLateCallbackCannotFaultExplicitRetry`.

`OnDisconnected`의 `ReferenceEquals(source, _client)`를 확인한다. ID 문자열 비교가 아니라 **실제로 같은 객체인지** 비교한다.

### SemaphoreSlim과 lock을 둘 다 쓰는 이유

| 도구 | 현재 보호 대상 | 기다림의 범위 |
| --- | --- | --- |
| `_operation` 세마포어 | ConnectAsync·DisconnectAsync·DisposeAsync 작업 순서 | 네트워크 await를 포함한 수명주기 작업 |
| `_stateLock`의 lock | 현재 클라이언트 참조·Snapshot 읽기/갱신 | 짧은 공유 상태 접근 |

lock 안에서 네트워크 작업을 await하는 구조가 아니다. 또한 `StateChanged` 수신자가 연결 메서드를 `.Wait()` 또는 `.Result`로 기다리면 교착 위험이 있으므로 콜백은 짧게 처리해야 한다.

## 6. 해제와 이벤트도 연결만큼 중요하다

`DisconnectCoreAsync`는 현재 클라이언트를 지역 변수에 담고 `_client = null`로 먼저 바꾼다. 이후 의도적인 해제에서 발생하는 이벤트가 “현재 연결의 사고”로 처리되지 않게 하기 위해서다. 끝에서는 `Dispose`로 자원을 정리한다.

`DisposeAsync`를 두 번 호출해도 안전하게 끝나는지는 `DisconnectFailureStillClosesTransportAndDisposeIsIdempotent`가 확인한다. 여기서 idempotent는 같은 해제 요청을 반복해도 중복 정리 때문에 잘못되지 않는 성질을 뜻한다.

| 헷갈리는 표현 | 여기서의 의미 |
| --- | --- |
| `Disconnected += handler` | C# 이벤트 수신자 등록. MQTT Topic 구독은 아님 |
| `Disconnected?.Invoke(...)` | 이벤트 수신자가 있으면 호출 |
| `StateChanged` 수신자 예외 격리 | 한 관측자의 오류가 연결 관리와 다른 관측자를 막지 않게 함 |
| `IIncomingMessageSink` 예외 전파 | COM-04의 Core 처리 장애는 잘못된 입력으로 숨기지 않음 |

마지막 두 행의 정책은 서로 다르다. “이벤트를 처리할 때 모든 예외를 무조건 삼킨다”로 일반화하지 않는다.

## 7. COM-03 테스트를 읽는 순서

아래 메서드는 모두 [ConnectionLifecycleTests.cs](tests/Amr.Communication.Tests/ConnectionLifecycleTests.cs)에 실제 존재한다.

| 순서 | 테스트 | 확인할 핵심 |
| --- | --- | --- |
| 1 | `SuccessAndExplicitDisconnectReportStateAndDisposeTransport` | 성공 → 해제의 상태 순서와 자원 정리 |
| 2 | `InvalidSettingsAreRejectedBeforeAnyTransport` | 포트·KeepAlive·Timeout 오류는 연결 전에 거부 |
| 3 | `TimeoutReturnsFailureAndReleasesResources` | 내부 제한 시간 경과 시 실패·해제 |
| 4 | `CallerCancellationIsDistinctFromFailure` | 호출자 취소는 연결 장애와 다른 Reason |
| 5 | `ConcurrentConnectsDoNotDuplicateSessions` | 겹친 연결 요청에서도 생성 1회 |
| 6 | `DisconnectDuringConnectWaitsThenClosesSession` | 연결 중 해제 요청이 들어오면 순서대로 처리 |
| 7 | `DisconnectBeforeConnectReturnsCannotPublishConnectedState` | await 이후 상태 재확인 |
| 8 | `FailedConnectionAndLateCallbackCannotFaultExplicitRetry` | 이전 세션의 늦은 이벤트 격리 |
| 9 | `UnexpectedDisconnectFaultsOnceWithoutAutomaticReconnect` | 중복 단절 알림·자동 재접속 여부 |
| 10 | `FailingObserverCannotBreakLifecycleOrOtherObservers` | 관측자 장애 격리 |
| 11 | `DisconnectFailureStillClosesTransportAndDisposeIsIdempotent` | 정리 실패·반복 해제·해제 후 재사용 |

각 테스트는 **준비 → 제품 메서드 실행 → 결과 확인**으로 읽는다. 파일 아래쪽의 `FakeClient`도 반드시 같이 본다. `Connect`·`Disconnect`에 어떤 함수를 넣었는지가 재현할 상황을 결정한다.

`TaskCompletionSource`는 비동기 작업 완료 시점을 테스트가 직접 정할 수 있게 한다. 예를 들어 `release.SetResult()` 전에는 연결을 끝내지 않고 두 번째 요청을 넣어 동시 호출 상황을 만든다. 테스트의 정해진 대기 시간은 실패 시 무한정 멈추지 않도록 쓰는 상한과, 제품 Timeout 설정을 구분해서 읽는다.

### 대역 테스트만으로 알 수 없는 것

FakeClient는 실제 패킷을 보내지 않는다. 따라서 실제 연결·PINGREQ/PINGRESP·응답 차단은 [BrokerVerification.cs](tools/Amr.Verification/BrokerVerification.cs)에서 별도로 확인한다.

| COM-03 verification | 실제 검증 도구가 보는 것 |
| --- | --- |
| 1 | 클라이언트 Connected와 Mosquitto의 연결 로그 |
| 2 | 테스트 KeepAlive 2초, 약 6초 업무 메시지 없는 연결 유지와 Ping 로그 |
| 3 | Broker 종료 감지 5초 이내, Broker 응답만 차단했을 때 감지 8초 이내 |
| 4 | 열리지 않은 포트의 최초 연결 실패, ConnectFailed 및 콘솔 종료 코드 2 |
| 5 | 자동 테스트·실제 시나리오·콘솔 프로세스 재시작 기록 |

이 값은 DN-03의 **개발 검증 기준**이다. 모든 네트워크에서 보장하는 운영 성능값은 아니다. 일반 콘솔 기본값은 KeepAlive 5초, Timeout 3초로 실증 설정과 다르다.

응답 차단 테스트는 [ResponseDropProxy.cs](tools/Amr.Verification/ResponseDropProxy.cs)를 사용하고, 차단 후 Broker에 직접 다시 접속해 Broker 자체는 살아 있는지도 확인한다. 프록시가 요청은 통과시키고 응답만 버리는지는 [ProxyTests.cs](tests/Amr.Verification.Tests/ProxyTests.cs)의 `DropModePreservesRequestsAndDiscardsResponses`가 검사한다.

## 8. COM-01 — 다음에 공부할 핵심

COM-03에서 연결 통로를 만들었다면 COM-01은 그 통로로 **올바른 메시지를 받고 보내며 처리 순서를 지키는 기능**이다.

| 필수 개념 | 왜 필요한가 |
| --- | --- |
| Publish / Subscribe | 누가 어떤 Topic으로 보내고 누가 받는지 구분 |
| 구독 완료 확인 | 연결 성공 직후 발행해 메시지를 놓치는 테스트 방지 |
| 수신과 명령 발행 경로 분리 | AMR에서 온 보고와 Core에서 온 요청을 혼동하지 않기 |
| AMR별 FIFO 처리 | 같은 로봇의 메시지가 앞선 처리를 추월하지 않게 하기 |
| 로봇 간 독립 처리 | 느린 로봇 하나가 다른 로봇을 막지 않게 하기 |
| 유한 큐와 과부하 정책 | 입력이 처리보다 빠를 때 메모리·지연을 관리하기 |
| QoS·중복·Retain | 전송 보장과 업무 처리 정책을 구분하기 |

### 먼저 읽을 현재 파일

| 파일 | 현재 상태와 읽는 목적 |
| --- | --- |
| [MqttSubscriber.cs](src/Amr.Communication/Mqtt/MqttSubscriber.cs) | SubscribeAsync는 주석만 있는 골격. 아직 실제 수신 진입점이 아님 |
| [MqttPublisher.cs](src/Amr.Communication/Mqtt/MqttPublisher.cs) | 발행 기능 골격 |
| [MqttTopics.cs](src/Amr.Communication/Mqtt/MqttTopics.cs) | 기존 Topic 문자열과 현재 요구사항 차이 확인 |
| [IBrokerClient.cs](src/Amr.Communication/Mqtt/IBrokerClient.cs) | 현재는 연결 API만 있음. 송수신에 필요한 경계를 설계해야 함 |
| [IncomingMessageProcessor.cs](src/Amr.Communication/Validation/IncomingMessageProcessor.cs) | 실제 수신 경로에서 재사용할 기존 검증·전달 경계 |
| [CONTRACT.md](src/Amr.Communication/Validation/CONTRACT.md) | 네 수신 유형의 현재 내부 계약 |
| [CommandMessage.cs](src/Amr.Communication/Models/CommandMessage.cs) | 아직 빈 모델. Command 필드는 DN-02에서 구체화해야 함 |
| [feature_list.json](feature_list.json) | COM-01의 8개 검증 항목과 DN-02/04/06 확인 |

`MqttClientService`의 내부 클라이언트는 private이다. 현재 골격에 함수 호출 몇 줄만 채우면 전체 송수신 구조가 완성되는 상태는 아니다. 어느 인터페이스에 송수신 동작을 둘지 먼저 설계해야 한다.

## 9. Topic과 Payload를 나눠 생각하기

다음은 **추적기에 적힌 목표 Topic**이다. 아래 표를 적었다고 실제 구독이 구현된 것은 아니다.

| 방향 | 목표 Topic | 의미 |
| --- | --- | --- |
| AMR → 관제 | `amr/{amrId}/telemetry` | 위치·속도·상태 |
| AMR → 관제 | `amr/{amrId}/alarm` | 알람 |
| AMR → 관제 | `amr/{amrId}/heartbeat` | 개별 AMR 생존 메시지 |
| AMR → 관제 | `amr/{amrId}/ack` | 명령 결과 응답 |
| 관제 → AMR | `amr/{amrId}/command` | MOVE·STOP·RESET 명령 |

여러 로봇의 Telemetry를 받는 필터 예는 `amr/+/telemetry`다. `+`는 한 Topic 구간, `#`는 여러 구간을 매칭한다. 와일드카드는 구독 필터에 쓰며 실제 발행 Topic에는 넣지 않는다. [MQTT 5 표준, 4.7](https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html)

기존 `MqttTopics`는 ACK에 `cmd/ack`, 명령에 `cmd`를 쓰고 `state`도 정의한다. COM-04의 수신 계약은 `ack`를 사용하고 별도의 `state`를 허용하지 않는다. **오래된 문자열 상수가 있다는 이유로 그 값을 요구사항으로 삼으면 안 된다.** COM-01에서 사용처와 테스트를 확인해 정리할 대상이다.

### 입력 방향은 둘이다

```text
[수신 목표]
AMR → Broker → 수신 콜백 → AMR 식별·처리 순서 관리
    → COM-04 검증·Core 전달 경계

[발행 목표]
Core의 Command 요청 → 대상 AMR·명령 계약 검증
    → command Topic으로 발행 → 해당 AMR 구독 클라이언트
```

수신 메시지는 기존 `IncomingMessageProcessor`를 거쳐야 한다. 발행 Command는 네 수신 유형에 포함되지 않으므로 해당 수신 검증기에 억지로 넣지 않는다. Core에서 받는 명령 입력을 위한 검증 계약이 별도로 필요하다.

## 10. AMR별 큐가 필요한 이유

FIFO는 먼저 들어온 항목을 먼저 꺼내는 순서다. 이 프로젝트의 요구는 여기에 **같은 AMR 처리끼리 겹치지 않음**까지 포함한다.

```text
도착: A1 → A2 → B1 → A3 → B2

AMR_A 작업자: [A1 처리 완료] → [A2 처리 완료] → [A3 처리 완료]
AMR_B 작업자: [B1 처리 완료] → [B2 처리 완료]
```

A1이 오래 걸려도 B1은 먼저 끝날 수 있어야 한다. 반면 A2는 A1이 끝나기 전에 시작하면 안 된다. 여기서 A1/A2는 학습용 메시지 번호이며 제품에 `seq` 필드가 이미 있다는 뜻은 아니다.

### 흔히 떠올리는 구현과 문제

| 접근 | 놓칠 수 있는 요구 |
| --- | --- |
| 메시지마다 Task.Run으로 처리 | 같은 AMR 메시지가 동시에 실행되거나 완료 순서가 바뀔 수 있음 |
| 전체 메시지를 하나의 작업자가 처리 | A의 느린 처리가 B까지 막음 |
| FIFO에서 꺼내기만 하고 처리 Task를 기다리지 않음 | 꺼낸 순서는 맞아도 실행이 겹침 |
| semaphore 하나로 모든 AMR을 묶음 | 다른 AMR의 독립 처리를 막음 |
| Payload ts로 정렬 | 요구한 도착 순서를 바꾸고 잘못된 장비 시각에 의존 |

COM-03의 세마포어는 **연결 수명주기 작업**을 직렬화한다. 그것만으로 COM-01의 **AMR별 메시지 큐**가 구현되지는 않는다.

### 공부할 구현 후보: Channel

.NET `Channel<T>`는 생산자와 소비자 사이에 데이터를 전달하는 도구다. 유한 크기로 만들 수 있고, 가득 찼을 때 기다리거나 버리는 모드를 정할 수 있다. `ReadAllAsync`로 비동기 소비를 구성할 수 있다. 이는 가능한 선택지이며 프로젝트에서 채택한 구현은 아직 아니다. [Microsoft Channels 설명](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)

이 프로젝트에 적용해 생각해 볼 **설계 제안**은 다음과 같다.

```text
수신 진입점:
  Topic 구조와 AMR ID를 확인한다.
  정상 형식의 ID에 해당하는 큐를 찾는다.
  해당 큐에 메시지를 넣는다.

AMR별 소비자:
  메시지를 하나 꺼낸다.
  기존 COM-04 Processor의 처리가 끝날 때까지 기다린다.
  그다음 메시지를 꺼낸다.
```

이것만으로 완성된 설계는 아니다. 특히 다음 사항을 함께 생각해야 한다.

- AMR 하나에 소비자 작업자가 중복 생성되지 않게 관리한다.
- 생산자가 여러 개라면 “도착 순서”를 어느 진입점에서 정할지 명시한다. Payload Timestamp로 대신하지 않는다.
- Callback에서 받은 메모리를 나중에 사용할 때는 버퍼 수명을 확인하고 필요한 경우 소유한 복사본을 만든다.
- Topic이 잘못됐다고 검증·실패 로그 없이 사라지면 안 된다. 기존 검증 경계를 통과하는 경로를 보장한다.
- 실제 Core가 예외를 던졌을 때 소비자 전체가 조용히 종료되지 않도록 실패·재시도·계속 처리 정책을 정한다. COM-04는 Core 예외를 호출자에게 전파한다.

### 큐가 가득 찼을 때가 더 중요하다

초당 1,000개가 들어오고 100개만 처리할 수 있다면 무제한 큐는 계속 커진다. 그렇다고 아무 메시지나 버리면 업무 의미가 달라진다.

Channel의 대기 방식은 입력 속도를 늦추는 backpressure를 만들 수 있다. 하지만 공유 MQTT 콜백에서 A의 큐가 빌 때까지 기다리면 **B의 입력까지 막을 수 있다.** 이 프로젝트에서 독립성을 만족하려면 큐 구성뿐 아니라 공통 수신 진입점의 대기 방식도 검증해야 한다.

큐 용량·대기/거부/드롭·AMR 큐 개수 상한·유휴 큐 정리·종료 시 남은 항목 처리 방식은 미결정이다. 장비 등록은 Core의 후속 범위이므로, Topic만 보고 무제한으로 새 큐를 생성하는 정책도 그대로 확정하지 않는다.

## 11. QoS와 ACK를 공부할 때의 구분

| MQTT QoS | 전송 수준의 기본 의미 |
| --- | --- |
| 0 | 최대 한 번, 유실 가능 |
| 1 | 최소 한 번, 중복 가능 |
| 2 | MQTT 프로토콜 교환에서 정확히 한 번 전달 |

QoS는 MQTT 전달 수준이다. 로봇 이동이나 DB 갱신까지 정확히 한 번 수행된다는 뜻은 아니다. 발행자가 Broker로 보내는 구간과 Broker가 구독자에게 보내는 구간도 구분해야 한다. [MQTT 5 표준, 4.3](https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html)

프로젝트에서는 다음 세 결과를 다른 것으로 취급해야 한다.

| 관찰한 결과 | 그 자체로 주장할 수 없는 것 |
| --- | --- |
| Broker 연결 성공 | 목표 Topic 구독이 완료됐음 |
| 발행 성공 또는 MQTT PUBACK | AMR이 명령을 수행했음 |
| 업무 ACK를 받아 검증 통과 | Core의 최종 업무 상태 갱신까지 완료됐음 |

`amr/{id}/ack`의 `cmdId`, `result`, `ts`는 프로젝트의 업무 메시지다. MQTT 패킷 식별자와 `cmdId`를 같은 것으로 취급하지 않는다. CommandId 생성·업무 결과 관리는 Core, 식별자 보존·전달은 Communication을 기본 제안으로 두고 DN-06에서 관리한다.

Retain은 새 구독자에게도 전달할 수 있도록 Topic의 메시지를 Broker에 보관하는 기능이다. [MQTT 5 표준, 3.3.1.3](https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html) 이 때문에 과거 명령이 새 구독 시 전달되는 상황을 어떻게 처리할지 검토해야 한다. 이 문서에서는 Command의 Retain·QoS·중복 정책을 확정하지 않는다.

## 12. COM-01 테스트는 무엇을 기준으로 만들까

기준은 [feature_list.json](feature_list.json)의 **COM-01 verification 1~8**이다. 아래는 요구사항에서 도출한 테스트 설계이며 아직 구현된 테스트 이름이나 실행 결과가 아니다.

| 항목 | 만들 테스트 상황 | 기대 관찰 | 검증 방식 |
| --- | --- | --- | --- |
| 1 | 실제 Broker 연결 후 네 수신 Topic 구독 | 구독 결과·Topic·QoS 기록 | 실제 Mosquitto |
| 2 | AMR 2대가 각각 정상 네 유형 발행 | AMR·유형·데이터가 Core 대역에 정확히 도착 | 실제 Mosquitto + AMR 클라이언트 + Core 대역 |
| 3 | 깨진 JSON·필드 누락·ID 불일치를 실제 발행 | 기존 COM-04 경로에서 로그만 남고 Core 0회 | 실제 Mosquitto |
| 4 | A1 처리를 멈춘 상태에서 A2 입력 | A1 완료 전 A2 시작 없음, 같은 AMR 중첩 없음 | 제어 가능한 처리 대역 |
| 5 | A1 처리를 멈춘 상태에서 B1 입력 | A1을 풀기 전에 B1 완료 | 제어 가능한 처리 대역 |
| 6 | MOVE·STOP·RESET 요청 | 대상 AMR의 command 구독에 도착, 다른 AMR에 미전달 | 실제 Mosquitto + 대상별 구독 |
| 7 | 잘못된 명령 타입·필드 요청 | 발행 전 거부·실패 이유, 전송 호출 0회 | 발행 대역 중심, 필요 시 실제 관찰 보강 |
| 8 | AMR 수신과 Core 명령 요청을 함께 실행 | 수신 검증·Core 전달과 대상 Topic 발행이 모두 완료 | 실제 Mosquitto + 동시 입력 |

### 순서 테스트는 단순히 “마지막 값”만 보면 부족하다

테스트용 처리기가 다음 기록을 남기게 설계해 본다.

```text
Start(A1)
Finish(A1)
Start(A2)
Finish(A2)
```

같은 AMR은 시작·종료 로그와 동시에 처리 중인 수의 최댓값이 1인지 함께 확인한다. 최종 저장 값만 맞으면 통과시키는 검사는 중간 순서 오류를 놓칠 수 있다.

### 고정된 sleep 대신 완료 신호로 상황 만들기

COM-03 테스트에서 배운 TaskCompletionSource를 응용한다. 아래는 앞으로 만들 테스트의 **절차 예시**다.

1. A1 처리가 시작됐다는 신호를 받는다.
2. A1은 테스트가 완료 신호를 주기 전까지 대기한다.
3. A2와 B1을 수신 처리기에 입력해 수락 여부를 확인한다.
4. A1을 풀기 전에 B1의 완료 신호를 받는다. A2는 시작하지 않았는지도 관찰한다.
5. A1 대기를 해제한다.
6. A1 완료 후 A2가 시작·완료되는 전체 기록과 최대 동시 처리 수를 검사한다.

대기가 무한정 지속되지 않도록 각 관찰에는 테스트용 상한을 둔다. “1초 잤으니 처리됐겠지”를 성공 판정의 근거로 삼지 않는다. 실제 Broker 테스트에서도 **구독 완료를 확인한 뒤** AMR이 발행하도록 준비한다.

### 통신 범위를 넘어가지 않기

Core 대역이 받은 메시지나 AMR 대역이 받은 Command를 검사했다고 실제 장비 이동·SQLite 업무 이력·GUI 갱신까지 통과한 것은 아니다. COM-01의 큐 테스트와 실제 송수신 테스트는 서로 보완하며 어느 하나만으로 다른 쪽을 대신하지 않는다.

## 13. COM-01 착수 전에 결정해야 할 내용

아래 목록은 공부용 체크리스트다. 지금 값을 임의로 정한 것은 아니다.

| 쟁점 | 기록할 곳 | 먼저 생각할 질문 |
| --- | --- | --- |
| Command 필드·자료형·허용 값 | DN-02, 관련 계약·revision | MOVE·STOP·RESET에 각각 무엇이 필수인가? |
| 수신·발행 Topic 정리 | COM-01, MqttTopics 사용처 | cmd와 command, cmd/ack와 ack 차이를 어떻게 정리할까? |
| Topic별 QoS·Retain·중복 | DN-04 | 중복이 들어오면 어디서 어떤 의미로 처리할까? |
| CommandId 경계 | DN-06 및 적용 계약 | 생성·전달·업무 결과 갱신 책임은 누구에게 있나? |
| 큐 제한과 느린 AMR | COM-01 설계 결정 | 다른 AMR 독립성을 유지하면서 과부하를 어떻게 알릴까? |
| 종료·단절 시 남은 메시지 | COM-01/DN-04 | 처리 중·대기 중 메시지는 어떻게 마무리할까? |
| 재접속·재구독 | DN-04, 후속 복구 범위 | 이번 범위에서 무엇을 지원하고 무엇을 후속으로 둘까? |

되돌릴 수 있는 개발 설정은 임시 결정으로 기록해 진행할 수 있다. 실제 장비 호환성과 업무 의미는 근거 없이 운영 확정값으로 만들지 않는다.

## 14. 공부하면서 실행할 명령

프로젝트 루트에서 실행하며 준비 조건은 [README.md](README.md)를 따른다. 아래는 사용자가 직접 실행할 학습 절차다. 이번 문서 작성에서는 제품 테스트·Broker 실행을 다시 수행하지 않았다.

### 공통 준비

```powershell
.\init.cmd check
.\init.cmd verify
```

### COM-03 대역 테스트만 실행 — Broker 불필요

```powershell
dotnet test tests/Amr.Communication.Tests/Amr.Communication.Tests.csproj --no-restore -m:1 -nr:false --filter "FullyQualifiedName~ConnectionLifecycleTests"
```

### 동시 연결 테스트 하나만 집중해서 보기

```powershell
dotnet test tests/Amr.Communication.Tests/Amr.Communication.Tests.csproj --no-restore -m:1 -nr:false --filter "FullyQualifiedName~ConcurrentConnectsDoNotDuplicateSessions"
```

`--no-restore`는 앞서 복원한 패키지를 쓰므로 첫 verify 이후에 사용한다. 빌드는 생략하지 않아 테스트 수정이 반영된다.

### 실제 연결 상태 보기 — Docker Desktop 필요

```powershell
.\init.cmd broker
.\init.cmd start -DurationSeconds 5
```

이 실행에서 Connected를 봤다고 COM-01 구독 완료로 해석하지 않는다. 기본 환경에서는 약 5초 유지 후 해제 흐름을 볼 수 있다. 콘솔 종료 코드는 정상 0, 설정 등 호스트 오류 1, 최초 연결 실패 2, 연결 후 단절 3이며 Ctrl+C 취소는 정상 종료로 처리한다.

### 실제 COM-03 시나리오 전체 검증

```powershell
.\init.cmd verify-broker
```

이 명령은 실제 테스트용 Mosquitto를 사용하고 결과를 evidence에 남긴다. 기존 개발 Broker를 임의로 끊어서 흉내 내기보다 정해진 검증 절차를 먼저 읽는다. 실제 COM-01 송수신 테스트는 아직 없으므로 실행 명령이나 통과 결과를 만들어 적지 않는다.

## 15. 직접 해 보면 좋은 학습 과제

아래 과제는 문서 작성 시 수행하지 않았다. 먼저 제품 코드 수정 없이 설명·관찰부터 한다.

| 단계 | 할 일 | 완료 기준 |
| --- | --- | --- |
| 1 | 성공·최초 실패·호출자 취소·연결 후 끊김의 상태표를 직접 그리기 | State와 Reason 차이를 설명할 수 있음 |
| 2 | ConnectAsync의 await 앞뒤에 중단점을 두고 기존 테스트 디버그 | 기다리는 동안 들어올 수 있는 이벤트를 설명 |
| 3 | 늦은 이벤트 테스트에서 A/B 객체를 구분해 보기 | ReferenceEquals가 왜 필요한지 설명 |
| 4 | MqttTopics와 추적기의 목표 Topic을 비교한 표 작성 | 수정해야 할 문자열과 수신 유형을 찾음 |
| 5 | A1·A2·B1 처리 순서를 종이에 그리기 | 같은 AMR 순서와 다른 AMR 독립성을 동시에 만족 |
| 6 | Channel로 숫자를 주고받는 별도 작은 실습을 구상 | 생산·소비·완료·취소·가득 찬 큐의 의미를 설명 |
| 7 | COM-01 항목 4·5의 테스트를 의사코드로 작성 | 어떤 신호·로그·Assert로 순서와 독립성을 판정할지 설명 |

추가로 공부할 C# 주제는 `Func`와 람다 → `Task`/`await` → `CancellationToken` → `IDisposable`/`IAsyncDisposable` → 이벤트 → `SemaphoreSlim`/lock → `TaskCompletionSource` → `Channel` 순서가 좋다. 클래스 문법을 모두 외운 뒤 시작하기보다 이 프로젝트의 한 사례에 연결해 익힌다.

## 16. 공식 문서를 읽을 때 볼 부분

2026-09-22에 확인한 공식 자료다. 라이브러리 예제의 API를 그대로 복사하기 전 현재 프로젝트 패키지 버전과 비교한다.

| 자료 | 볼 범위 | 프로젝트에 연결할 질문 |
| --- | --- | --- |
| [MQTT 5 OASIS 표준](https://docs.oasis-open.org/mqtt/mqtt/v5.0/os/mqtt-v5.0-os.html) | 3.1.2.4 Clean Start, 3.3.1.3 Retain, 4.3 QoS, 4.7 Topic | 세션·전송 보장·업무 ACK는 어떻게 다른가? |
| [Microsoft C# 비동기 프로그래밍](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/) | 순차 await와 여러 작업의 완료 관리 | async가 붙어도 왜 순서·동시성을 별도로 설계해야 하나? |
| [Microsoft .NET Channels](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels) | 유한 채널, 가득 찼을 때의 동작, 읽기·쓰기·완료 | 입력이 처리보다 빠르면 어떻게 되는가? |

MQTT 규격은 프로토콜 사실을 설명하고, 우리 프로젝트의 큐·예외·업무 정책은 추적기와 코드가 정한다. 공식 문서의 예제가 곧 이 프로젝트의 채택된 설계는 아니다.

## 17. 이해 확인 질문

- ConnectAsync가 true를 반환해도 COM-01이 준비됐다고 할 수 없는 이유는?
- 연결이 끝나기 직전에 끊길 수 있다면 await 뒤 어디를 다시 검사해야 하나?
- 내부 Timeout과 호출자 취소는 현재 코드에서 어떻게 구분하나?
- 이전 클라이언트의 끊김 알림이 새 연결을 망치지 않는 이유는?
- 같은 AMR에 작업자 두 개를 두면 FIFO 큐만으로 충분한가?
- A 큐가 꽉 찼을 때 공유 수신 콜백이 기다리면 B에는 어떤 영향이 생길까?
- 순서 테스트에서 마지막 값뿐 아니라 시작·종료 로그가 필요한 이유는?
- MQTT PUBACK과 업무 ACK의 cmdId는 왜 구분해야 하나?
- Core가 예외를 던졌는데 소비자 작업이 종료됐다면 무엇을 기록하고 결정해야 하나?

우선 **COM-03의 동시 연결 테스트를 설명하고, COM-01의 A1·A2·B1 순서 테스트를 설계할 수 있는 수준**을 목표로 한다.
