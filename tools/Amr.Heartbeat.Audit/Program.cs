using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Amr.Communication.Models;
using Amr.Communication.Monitoring;
using Amr.Communication.Mqtt;
using Amr.Communication.Validation;
using MQTTnet;

// 실제 Mosquitto + 고유 ID의 AMR 발행 대역 + Core/알림 대역으로 COM-02를 검증합니다.
// dotnet run --project tools/Amr.Heartbeat.Audit -- HOST PORT NEW_OUTPUT_DIR
if (args.Length != 3) throw new ArgumentException("Expected HOST PORT NEW_OUTPUT_DIR");
var output = Path.GetFullPath(args[2]);
if (Directory.Exists(output)) throw new ArgumentException("Use a new evidence directory for each run.");
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions { WriteIndented = true };
var prefix = "HB_" + Guid.NewGuid().ToString("N")[..10];
var a = prefix + "_A"; var b = prefix + "_B";
var heartbeatOptions = new HeartbeatMonitorOptions { Timeout = TimeSpan.FromMilliseconds(1500), CheckInterval = TimeSpan.FromMilliseconds(100) };
var brokerOptions = new BrokerConnectionOptions { Host = args[0], Port = int.Parse(args[1]) };
var sink = new AuditSink(a, b);
var checks = new List<object>();
var failed = 0; var completed = false; string? error = null;
void Check(string name, bool pass, string expected, object actual)
{
    checks.Add(new { name, executed_at = DateTimeOffset.Now, result = pass ? "passed" : "failed", expected, actual });
    if (!pass) failed++;
    Console.WriteLine($"{(pass ? "PASS" : "FAIL")} {name}: {JsonSerializer.Serialize(actual)}");
}
static async Task Until(Func<bool> condition, int timeoutMilliseconds = 5000)
{
    var watch = Stopwatch.StartNew();
    while (!condition())
    {
        if (watch.ElapsedMilliseconds >= timeoutMilliseconds) throw new TimeoutException("Expected observation did not arrive.");
        await Task.Delay(20);
    }
}
int Events(string id, HeartbeatNotificationKind kind) => sink.Events.Count(e => e.AmrId == id && e.Kind == kind);
using var peer = new MqttClientFactory().CreateMqttClient();
await using var manager = new MqttConnectionManager(brokerOptions, new IncomingMessageProcessor(sink, sink, sink), heartbeatOptions, sink);
using var publishing = new CancellationTokenSource();
Task? producer = null;
var aMode = 0; // 0: 정상, 1: status 필드 없는 비정상 메시지만 발행
try
{
    await peer.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer(brokerOptions.Host, brokerOptions.Port)
        .WithClientId(prefix + "_publisher").Build());
    if (!await manager.ConnectAsync()) throw new InvalidOperationException("Observer could not connect.");
    await Task.Delay(1800);
    Check("no_timeout_before_first_heartbeat", sink.Events.IsEmpty, "No inferred AMRs before first valid heartbeat", sink.Events.Count);

    producer = Task.Run(async () =>
    {
        try
        {
            while (!publishing.IsCancellationRequested)
            {
                // 과거/미래 Payload ts를 의도적으로 사용합니다. 실제 감시는 관제 수신 시각이어야 합니다.
                var aPayload = Volatile.Read(ref aMode) == 0
                    ? JsonSerializer.Serialize(new { id = a, ts = 1L, status = "OK" })
                    : JsonSerializer.Serialize(new { id = a, ts = long.MaxValue });
                await peer.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"amr/{a}/heartbeat").WithPayload(aPayload).Build(), publishing.Token);
                await peer.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"amr/{b}/heartbeat")
                    .WithPayload(JsonSerializer.Serialize(new { id = b, ts = long.MaxValue, status = "OK" })).Build(), publishing.Token);
                await Task.Delay(200, publishing.Token);
            }
        }
        catch (OperationCanceledException) when (publishing.IsCancellationRequested) { }
    });
    await Until(() => Events(a, HeartbeatNotificationKind.MonitoringStarted) == 1 && Events(b, HeartbeatNotificationKind.MonitoringStarted) == 1);
    await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    await Task.Delay(3300);
    Check("two_amrs_refresh_while_core_a_blocked", sink.Events.All(e => e.Kind != HeartbeatNotificationKind.TimedOut)
        && sink.Delivered.Count(m => m.AmrId == b) >= 5 && sink.Delivered.All(m => m.AmrId != a),
        "Both heartbeat clocks refresh despite A Core being blocked for over twice the timeout", new { notifications = sink.Events.ToArray(), deliveredB = sink.Delivered.Count(m => m.AmrId == b) });
    sink.Release.TrySetResult();

    Interlocked.Exchange(ref aMode, 1);
    await Until(() => Events(a, HeartbeatNotificationKind.TimedOut) == 1);
    await Task.Delay(800);
    var timeout = sink.Events.First(e => e.AmrId == a && e.Kind == HeartbeatNotificationKind.TimedOut);
    Check("only_a_times_out_despite_invalid_heartbeats", Events(a, HeartbeatNotificationKind.TimedOut) == 1
        && Events(b, HeartbeatNotificationKind.TimedOut) == 0 && sink.Invalid.Count >= 3 && manager.IsConnected,
        "Invalid A input cannot refresh; healthy B and Broker stay connected; no duplicate timeout", new { timeout, invalid = sink.Invalid.Count, brokerConnected = manager.IsConnected });
    Check("timeout_event_contains_receipt_and_timing", timeout.ElapsedMilliseconds >= 1500 && timeout.ElapsedMilliseconds <= 2100
        && timeout.LastReceivedUnixMilliseconds > 1_700_000_000_000 && timeout.TimeoutMilliseconds == 1500,
        "1500ms deadline + 100ms poll + 500ms scheduling tolerance; local receipt time, not payload ts", timeout);

    Interlocked.Exchange(ref aMode, 0);
    await Until(() => Events(a, HeartbeatNotificationKind.Recovered) == 1);
    await Task.Delay(600);
    Check("valid_heartbeat_recovers_once", Events(a, HeartbeatNotificationKind.Recovered) == 1 && Events(a, HeartbeatNotificationKind.TimedOut) == 1,
        "One recovery notification when normal heartbeats resume", sink.Events.Where(e => e.AmrId == a).ToArray());

    await manager.DisconnectAsync();
    var beforeStop = sink.Events.Count;
    await Task.Delay(1800);
    Check("disconnect_stops_monitor", sink.Events.Count == beforeStop && peer.IsConnected,
        "No AMR timeout notifications after deliberate receiver disconnect", new { beforeStop, afterStop = sink.Events.Count });
    if (!await manager.ConnectAsync()) throw new InvalidOperationException("Explicit reconnect failed.");
    await Until(() => Events(a, HeartbeatNotificationKind.MonitoringStarted) == 2 && Events(b, HeartbeatNotificationKind.MonitoringStarted) == 2);
    Check("reconnect_starts_fresh_monitor", Events(a, HeartbeatNotificationKind.Recovered) == 1,
        "New session learns both AMRs anew instead of reusing old timeout state", sink.Events.ToArray());

    // 같은 시험용 ClientId만 중복 접속해 단절시킵니다. 기존 개발 Broker/사용자 클라이언트는 유지합니다.
    using var replacement = new MqttClientFactory().CreateMqttClient();
    await replacement.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer(brokerOptions.Host, brokerOptions.Port)
        .WithClientId(brokerOptions.ClientId).Build());
    await Until(() => manager.Snapshot.State == BrokerConnectionState.Faulted);
    var beforeLossWait = sink.Events.Count;
    await Task.Delay(1800);
    Check("broker_loss_is_not_amr_timeout", sink.Events.Count == beforeLossWait && manager.Snapshot.Reason == "ConnectionLost",
        "Broker disconnection stops AMR timers and reports ConnectionLost separately", new { manager.Snapshot, eventsBefore = beforeLossWait, eventsAfter = sink.Events.Count });
    await replacement.DisconnectAsync();
    completed = true;
}
catch (Exception ex) { error = ex.ToString(); Console.Error.WriteLine(error); }
finally
{
    publishing.Cancel(); sink.Release.TrySetResult();
    if (producer is not null)
    {
        try { await producer; }
        catch (Exception ex) { error ??= ex.ToString(); completed = false; }
    }
    await manager.DisconnectAsync();
    if (peer.IsConnected) await peer.DisconnectAsync();
    await File.WriteAllTextAsync(Path.Combine(output, "notifications.json"), JsonSerializer.Serialize(sink.Events.ToArray(), json));
    await File.WriteAllTextAsync(Path.Combine(output, "validation-errors.json"), JsonSerializer.Serialize(sink.Invalid.ToArray(), json));
    await File.WriteAllTextAsync(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
    {
        executed_at = DateTimeOffset.Now, completed, failed_checks = failed, error,
        environment = new { brokerOptions.Host, brokerOptions.Port, heartbeatOptions.Timeout, heartbeatOptions.CheckInterval,
            amrA = a, amrB = b, publicationMilliseconds = 200, mqttnet = "5.2.0.1603", testDoubles = "AMR publisher and gated Core/event sink; real Mosquitto transport" }, checks
    }, json));
}
return completed && failed == 0 ? 0 : 1;

sealed class AuditSink(string blockedAmr, string otherAmr) : IIncomingMessageSink, IValidationLog, IMessageProcessingLog, IHeartbeatEventSink
{
    // 같은 개발 Broker에서 실행 중인 다른 실습 AMR은 이 검증의 결과에 섞지 않습니다.
    private bool OwnAmr(string id) => id == blockedAmr || id == otherAmr;
    public ConcurrentQueue<HeartbeatNotification> Events { get; } = new();
    public ConcurrentQueue<IncomingMessage> Delivered { get; } = new();
    public ConcurrentQueue<ValidationFailureLog> Invalid { get; } = new();
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async ValueTask ReceiveAsync(IncomingMessage message, CancellationToken token)
    {
        if (!OwnAmr(message.AmrId)) return;
        if (message.AmrId == blockedAmr) { Entered.TrySetResult(); await Release.Task.WaitAsync(token); }
        Delivered.Enqueue(message);
    }
    public void OnHeartbeatChanged(HeartbeatNotification notification)
    {
        if (OwnAmr(notification.AmrId)) Events.Enqueue(notification);
    }
    public void Rejected(ValidationFailureLog failure)
    {
        if (failure.Topic == $"amr/{blockedAmr}/heartbeat" || failure.Topic == $"amr/{otherAmr}/heartbeat") Invalid.Enqueue(failure);
    }
    public void Failed(MessageProcessingFailure failure) => Console.WriteLine(JsonSerializer.Serialize(failure));
}
