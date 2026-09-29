// Channel은 넣는 쪽과 꺼내는 쪽이 안전하게 함께 사용하는 대기열(택배 보관함)입니다.
using System.Threading.Channels;
using Amr.Communication.Models;
using Amr.Communication.Monitoring;
using Amr.Communication.Validation;

namespace Amr.Communication.Mqtt;

// 연결 세션이 소유합니다. 같은 AMR은 순차 처리하고 다른 AMR은 별도로 진행합니다.
// 택배 분류소 비유: AMR마다 보관함(Queue) 하나와 꺼내는 작업(ConsumeAsync) 하나를 둡니다.
// internal/sealed는 프로젝트 내부에서 사용하고 상속하지 않는 클래스라는 뜻입니다.
// 두 Dispose 인터페이스는 정리 요청만 하는 방법과 정리 완료까지 기다리는 방법을 제공합니다.
internal sealed class AmrMessageDispatcher : IDisposable, IAsyncDisposable
{
    // 봉투 검사와 실제 수신자 호출을 맡습니다. 현재 콘솔 실행의 수신자는 ConsoleMessageSink입니다.
    private readonly IncomingMessageProcessor _processor;
    private readonly HeartbeatMonitor? _heartbeats;
    // AMR 하나의 보관함에서 기다릴 수 있는 메시지 수입니다. 지금 처리 중인 한 건은 별도입니다.
    private readonly int _capacity;
    // 한 연결에서 만들 수 있는 AMR 보관함의 최대 개수입니다.
    private readonly int _maxAmrs;
    // 여러 접수 요청이 같은 명부를 동시에 고치지 않게 하는 잠금용 열쇠입니다.
    private readonly object _gate = new();
    // "AMR ID → 보관함과 처리 작업" 명부입니다. Ordinal은 ID의 대소문자를 구분해 비교합니다.
    private readonly Dictionary<string, Worker> _workers = new(StringComparer.Ordinal);
    // 모든 담당 작업에 "이제 그만하자"고 알리는 취소 신호 발신기입니다.
    private readonly CancellationTokenSource _stop = new();
    // 종료 시작 표시입니다. volatile은 여러 스레드가 이 표시를 읽고 쓰는 데 쓰이며, 잠금 전체를 대신하지 않습니다.
    private volatile bool _stopping;
    // 종료 작업을 보관합니다. ?는 아직 종료 요청이 없어 null일 수 있다는 뜻입니다.
    private Task? _shutdown;

    // 생성자: 기본으로 AMR당 256건 대기, 한 연결당 최대 64개 AMR을 허용합니다(개발용 설정).
    public AmrMessageDispatcher(IncomingMessageProcessor processor, int capacity = 256, int maxAmrs = 64,
        HeartbeatMonitor? heartbeats = null)
    {
        // 검사/전달 담당자가 없으면 바로 잘못된 설정이라고 알립니다.
        ArgumentNullException.ThrowIfNull(processor);
        // 보관함 크기는 1 이상이어야 합니다.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        // 허용 AMR 수도 1 이상이어야 합니다.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAmrs);
        // 받은 세 값을 각각 대응하는 필드에 저장합니다(튜플 대입).
        (_processor, _capacity, _maxAmrs) = (processor, capacity, maxAmrs);
        _heartbeats = heartbeats;
    }

// 큐잉된 메시지를 처리하고, Core 경계의 수신자에게 전달합니다.// AMR별로 순차 처리, 다른 AMR은 병렬 처리
    // [2] 분류: 이 메서드 자체는 동기적으로 검사·접수합니다. 실제 전달은 [3]의 별도 작업이 수행합니다.
    // topic은 배송 주소, payload는 읽기 전용 바이트 묶음입니다. true는 "큐에 넣음"을 뜻합니다.
    public bool TryEnqueue(string topic, ReadOnlyMemory<byte> payload)
    {
        // 큐에서 기다린 시간이 아닌, 수신 검사 직전의 관제 시각을 잡습니다.
        var received = _heartbeats?.CaptureReceipt();
        // 주소·JSON·필수 항목 등을 검사합니다. 실패 로그는 Validate 내부에서 남깁니다.
        var result = _processor.Validate(topic, payload);
        // 검사 실패 메시지는 보관함에 넣지 않고 접수를 끝냅니다.
        if (!result.IsValid) return false;
        // 정상 메시지를 꺼냅니다. !는 앞의 검사를 근거로 null이 아니라고 컴파일러에 알려주는 표시입니다.
        var message = result.Message!;
        // 접수를 거절해야 할 경우 사용할 짧은 오류 이름입니다.
        string code;
        // 사람이 읽을 거절 이유입니다.
        string reason;
        // 명부 확인·생성·큐 넣기 동안만 잠급니다. Core 처리 완료를 기다리며 이 잠금을 잡지는 않습니다.
        lock (_gate)
        {
            // 이미 종료 작업이 만들어졌다면 새 메시지를 받지 않습니다.
            if (_shutdown is not null)
                // 오류 이름과 설명을 함께 저장합니다.
                (code, reason) = ("ReceiverStopped", "Connection session is stopping; message not queued.");
            // 종료 중이 아니면 해당 AMR의 보관함을 찾습니다.
            else
            {
                // TryGetValue는 명부를 찾아 out worker에 결과를 담습니다. &&는 두 조건을 모두 만족해야 합니다.
                // 처음 보는 AMR이고 명부에 자리가 남았을 때만 새 담당자를 만듭니다.
                if (!_workers.TryGetValue(message.AmrId, out var worker) && _workers.Count < _maxAmrs)
                {
                    // 이 AMR의 보관함을 만듭니다.
                    worker = new Worker(_capacity);
                    // 다음 메시지도 같은 보관함을 쓰도록 AMR ID와 연결해 명부에 올립니다.
                    _workers.Add(message.AmrId, worker);
                    // AMR별 소비 작업을 딱 하나 시작합니다. () =>는 "이 일을 실행해 줘"라는 함수 표현입니다.
                    // Task.Run은 스레드 풀에 맡깁니다. AMR마다 전용 스레드를 하나씩 만드는 것은 아닙니다.
                    worker.Task = Task.Run(() => ConsumeAsync(worker));
                }
                // 새 AMR인데 허용 개수가 꽉 찼다면 담당자를 만들지 못한 상태입니다.
                if (worker is null)
                    // 기존 AMR의 큐는 계속 사용할 수 있고, 이 새 AMR의 메시지만 거절합니다.
                    (code, reason) = ("AmrLimitReached", "AMR queue limit reached for this connection session.");
                // 주소와 메시지를 한 묶음으로 넣습니다. 성공하면 true로 끝내고, 꽉 찼으면 기다리지 않습니다.
                else
                {
                    // 큐가 꽉 차도 유효한 Heartbeat가 도착했다는 사실은 먼저 기록합니다.
                    if (message is HeartbeatMessage && received is { } receipt)
                        _heartbeats!.Observe(message.AmrId, receipt);
                    if (worker.Queue.Writer.TryWrite(new(topic, message))) return true;
                    // 새 메시지를 거절합니다. 먼저 들어간 메시지를 밀어내는 정책은 아닙니다.
                    (code, reason) = ("QueueFull", "AMR queue is full; new message rejected without blocking other AMRs.");
                }
            }
        }
        // 명부 잠금을 푼 다음 거절 이유를 남깁니다. JSON 검사 실패와는 별도의 처리 오류 로그입니다.
        _processor.ProcessingFailed(topic, code, reason);
        // 호출자에게 큐 접수 실패를 알립니다.
        return false;
    }

    // [3] 처리: AMR 하나를 맡은 작업입니다. A 작업이 기다려도 B 작업은 자기 보관함을 처리할 수 있습니다.
    // async Task는 완료를 기다릴 수 있는 비동기 작업입니다. async 자체가 새 스레드를 만드는 것은 아닙니다.
    private async Task ConsumeAsync(Worker worker)
    {
        // 종료 신호를 읽을 수 있는 토큰을 받습니다. 토큰은 강제 종료 버튼이 아니라 협조 요청입니다.
        var token = _stop.Token;
        // 큐를 기다리다 취소되어도 아래 finally에서 남은 메시지를 정리하도록 감쌉니다.
        try
        {
            // 메시지가 오면 하나씩 꺼냅니다. 비어 있으면 반복 조회로 CPU를 쓰지 않고 비동기로 기다립니다.
            // ConfigureAwait(false)는 이어질 작업이 원래 실행 문맥으로 돌아오도록 요구하지 않습니다.
            await foreach (var item in worker.Queue.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                // 꺼내는 사이 종료가 시작됐을 수도 있으므로 실제 전달 직전에 다시 확인합니다.
                if (_stopping || token.IsCancellationRequested)
                {
                    // 방금 꺼낸 메시지는 전달하지 않고, 종료 때문에 버렸다는 기록을 남깁니다.
                    _processor.ProcessingFailed(item.Topic, "ShutdownDiscarded", "Queued message discarded during session shutdown.");
                    // 이 AMR의 반복 처리를 끝내고 finally로 이동합니다.
                    break;
                }
                // 실제 수신자에게 전달하고 이 한 건의 완료를 기다립니다. 그래서 같은 AMR의 순서가 유지됩니다.
                // 현재 수신자는 콘솔 출력 대역이며, 추후 실제 Core도 이 경계로 연결할 수 있습니다.
                try { await _processor.DeliverAsync(item.Message, token).ConfigureAwait(false); }
                // 예외 중에서도 "우리 종료 신호에 따른 취소"일 때만 이 구간에서 처리합니다.
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // 처리 중이던 메시지가 종료 요청으로 취소됐다고 기록합니다.
                    _processor.ProcessingFailed(item.Topic, "ProcessingCancelled", "Core processing cancelled during session shutdown.");
                    // 취소 후에는 다음 메시지를 전달하지 않습니다.
                    break;
                }
                // 종료 취소가 아닌 일반적인 수신자 오류는 여기서 받습니다.
                catch (Exception error)
                {
                    // 오류 종류만 기록합니다. break가 없으므로 다음 메시지는 계속 처리합니다.
                    _processor.ProcessingFailed(item.Topic, "HandlerFailed", error.GetType().Name);
                }
            }
        }
        // 빈 큐에서 기다리는 중 종료 신호를 받은 경우도 정상적인 종료로 받아들입니다.
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        // 정상 종료·취소·오류 중 어떤 경로로 나가든 마지막 정리를 수행합니다.
        finally
        {
            // 남아 있어서 버리는 메시지 수를 셉니다.
            var discarded = 0;
            // 버린 메시지 중 마지막 주소를 로그용으로 기억합니다. 없으면 null입니다.
            string? topic = null;
            // 더 기다리지 않고 현재 남아 있는 메시지를 모두 꺼내 버립니다. Core에는 전달하지 않습니다.
            while (worker.Queue.Reader.TryRead(out var item)) { discarded++; topic = item.Topic; }
            // 실제로 버린 메시지가 있을 때만 개수를 기록합니다.
            if (discarded > 0)
                // $"...{값}..."은 문장 안에 변수 값을 넣는 C# 문자열 문법입니다.
                _processor.ProcessingFailed(topic, "ShutdownDiscarded", $"Discarded {discarded} queued messages during session shutdown.");
        }
    }

    // 분류소의 영업 종료 준비에 해당하는 공통 작업입니다. 여러 번 불러도 같은 종료 Task를 돌려줍니다.
    private Task RequestStop()
    {
        // 신규 보관함 생성과 종료 시작이 동시에 명부를 바꾸지 않도록 같은 열쇠를 사용합니다.
        lock (_gate)
        {
            // 이미 종료 중이면 새 정리 작업을 만들지 않습니다.
            if (_shutdown is not null) return _shutdown;
            // 소비자가 다음 메시지를 전달하기 전에 종료 상태를 볼 수 있도록 표시합니다.
            _stopping = true;
            // Broker 연결이 끝난 뒤 개별 AMR Timeout이 뒤늦게 나오지 않도록 먼저 감시를 닫습니다.
            _heartbeats?.Dispose();
            // 지금까지 등록된 담당자 목록을 배열로 복사합니다.
            var workers = _workers.Values.ToArray();
            // 각 보관함의 투입구를 닫습니다. 이미 들어 있는 메시지는 소비자의 finally에서 정리합니다.
            foreach (var worker in workers) worker.Queue.Writer.TryComplete();
            // 동기 Dispose에서도 Core의 취소 콜백/작업 완료로 수신 스레드를 막지 않습니다.
            // 정리도 별도 작업으로 실행하고, 그 작업을 _shutdown에 보관합니다.
            _shutdown = Task.Run(async () =>
            {
                // 처리 중인 수신자와 큐 대기자에게 취소 신호를 보냅니다. 협조하는 작업이 중단됩니다.
                try { await _stop.CancelAsync().ConfigureAwait(false); }
                // 취소 알림을 받는 코드가 오류를 내더라도 남은 작업의 종료 관찰을 계속합니다.
                catch (Exception error) { _processor.ProcessingFailed(null, "CancellationFailed", error.GetType().Name); }
                // 모든 AMR 담당자가 끝날 때까지 기다립니다. 취소를 무시하는 작업은 여기서 계속 관찰합니다.
                try { await Task.WhenAll(workers.Select(worker => worker.Task)).ConfigureAwait(false); }
                // 작업들이 끝나면 취소 신호 발신기의 자원을 정리합니다.
                finally { _stop.Dispose(); }
            });
            // 호출자가 필요하면 이 Task를 await하여 정리 완료를 기다릴 수 있습니다.
            return _shutdown;
        }
    }

    // 외부에서 정리 완료를 기다리는 진입점입니다. default는 별도의 대기 취소 신호가 없다는 뜻입니다.
    public Task StopAsync(CancellationToken cancellationToken = default)
        // =>는 짧은 메서드 본문 표기입니다. WaitAsync의 취소는 "대기"를 멈추며 작업을 강제 종료하지 않습니다.
        => RequestStop().WaitAsync(cancellationToken);
    // _ =는 반환 Task를 여기서 기다리지 않는다는 표시입니다. 실제 정리는 RequestStop 안에서 계속됩니다.
    public void Dispose() => _ = RequestStop();
    // await using 등 비동기 정리를 쓰는 호출자는 이 ValueTask로 종료 완료를 기다릴 수 있습니다.
    public ValueTask DisposeAsync() => new(RequestStop());

    // record는 값을 묶는 자료형입니다. 한 택배에 원래 주소와 검사된 메시지를 함께 보관합니다.
    private sealed record QueuedMessage(string Topic, IncomingMessage Message);
    // Worker는 AMR 하나의 보관함과 담당 작업을 묶습니다. (int capacity)는 생성자 매개변수입니다.
    private sealed class Worker(int capacity)
    {
        // 크기가 정해진 Channel을 만듭니다. Writer는 넣는 입구, Reader는 꺼내는 출구입니다.
        public Channel<QueuedMessage> Queue { get; } = Channel.CreateBounded<QueuedMessage>(new BoundedChannelOptions(capacity)
        {
            // 읽기는 한 작업만, 쓰기는 여러 호출을 허용합니다. 넣는 순간 소비자 코드를 같은 호출에서 실행하지 않습니다.
            SingleReader = true, SingleWriter = false, AllowSynchronousContinuations = false,
            // Wait 설정이라도 위에서는 기다리는 WriteAsync 대신 즉시 성공/실패를 반환하는 TryWrite를 사용합니다.
            FullMode = BoundedChannelFullMode.Wait // TryWrite만 사용하므로 큐가 가득 차면 기다리지 않습니다.
        });
        // 생성 직후에는 완료된 빈 Task를 두고, 첫 접수 때 실제 ConsumeAsync 작업으로 교체합니다.
        public Task Task { get; set; } = Task.CompletedTask;
    }
}
