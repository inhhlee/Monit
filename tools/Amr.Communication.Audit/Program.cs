using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Amr.Communication.Models;
using Amr.Communication.Mqtt;
using Amr.Communication.Validation;
using MQTTnet;
using MQTTnet.Formatter;

// Explicit diagnostic runner, outside the solution's normal test pipeline.
// Exit 0 means evidence collection completed; individual checks retain passed/failed.
// Usage: dotnet run --project tools/Amr.Communication.Audit -- HOST PORT OUTPUT_DIR
if (args.Length != 3) throw new ArgumentException("Expected HOST PORT OUTPUT_DIR");
var output = Path.GetFullPath(args[2]);
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions { WriteIndented = true };
var checks = new List<object>();
var failed = 0;
void Check(string name, bool success, string expected, object actual)
{
    checks.Add(new { name, executed_at = DateTimeOffset.Now, result = success ? "passed" : "failed", expected, actual });
    if (!success) failed++;
    Console.WriteLine($"{(success ? "PASS" : "FAIL")} {name}: {JsonSerializer.Serialize(actual)}");
}
static async Task<bool> Wait(Func<bool> condition, int milliseconds = 3000)
{
    var started = Stopwatch.StartNew();
    while (!condition() && started.ElapsedMilliseconds < milliseconds) await Task.Delay(20);
    return condition();
}
var prefix = "AUDIT_" + Guid.NewGuid().ToString("N")[..10];
var a = prefix + "_A";
var b = prefix + "_B";
var sink = new RecordingSink(prefix);
var validationLog = new RecordingLog(prefix);
var received = new ConcurrentQueue<(string topic, string payload)>();
using var peer = new MqttClientFactory().CreateMqttClient();
peer.ApplicationMessageReceivedAsync += e =>
{
    received.Enqueue((e.ApplicationMessage.Topic, e.ApplicationMessage.ConvertPayloadToString()));
    return Task.CompletedTask;
};
var options = new BrokerConnectionOptions { Host = args[0], Port = int.Parse(args[1]) };
await using var manager = new MqttConnectionManager(options, new IncomingMessageProcessor(sink, validationLog));
var collection = "failed";
string? error = null;
try
{
    await peer.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer(options.Host, options.Port)
        .WithProtocolVersion(MqttProtocolVersion.V500).WithClientId(prefix).WithCleanStart().Build());
    await peer.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
        .WithTopicFilter($"amr/{a}/cmd").WithTopicFilter($"amr/{a}/command")
        .WithTopicFilter($"amr/{b}/cmd").WithTopicFilter($"amr/{b}/command").Build());
    if (!await manager.ConnectAsync()) throw new InvalidOperationException("Communication manager could not connect/subscribe");
    async Task Publish(string id, string kind, string payload) =>
        await peer.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"amr/{id}/{kind}").WithPayload(payload).Build());
    string Heartbeat(string id, long ts) => JsonSerializer.Serialize(new { id, ts, status = "OK" });

    foreach (var id in new[] { a, b })
    {
        await Publish(id, "heartbeat", Heartbeat(id, 101));
        await Publish(id, "telemetry", JsonSerializer.Serialize(new { id, ts = 102L, pos = new { x = 1.25, y = -2.5 }, sp = 0.8, state = "BUSY" }));
        await Publish(id, "alarm", JsonSerializer.Serialize(new { id, ts = 103L, code = "ALM-AUDIT", level = "WARN", alarmState = "ACTIVE" }));
        await Publish(id, "ack", JsonSerializer.Serialize(new { id, ts = 104L, cmdId = id + "-ack", result = "FAILURE", reason = "AUDIT_FAILURE" }));
    }
    await Wait(() => sink.Messages.Count == 8);
    Check("two_amrs_four_message_types", sink.Messages.Count == 8 && new[] { a, b }.All(id =>
        sink.Messages.Count(x => x.AmrId == id) == 4), "All four types for each of two AMRs reach the Core test sink", sink.Messages.Select(x => new { x.AmrId, x.Kind, x.TimestampUnixMilliseconds }).ToArray());
    Check("typed_values_preserved", sink.Messages.OfType<TelemetryMessage>().Count(x => x.Position.X == 1.25 && x.Position.Y == -2.5 && x.Speed == 0.8 && x.State == AmrState.Busy) == 2
        && sink.Messages.OfType<AlarmMessage>().Count(x => x.Level == AlarmLevel.Warning && x.AlarmState == AlarmState.Active && x.Message is null) == 2
        && sink.Messages.OfType<AckMessage>().Count(x => x.Result == AckResult.Failure && x.CommandId == x.AmrId + "-ack" && x.Reason == "AUDIT_FAILURE") == 2,
        "Telemetry/Alarm fields and FAILURE ACK identifiers survive transport and validation", "See typed-messages.json");

    await Publish(a, "heartbeat", "{");
    await Publish(a, "heartbeat", JsonSerializer.Serialize(new { id = a, ts = 201L }));
    await Publish(a, "heartbeat", Heartbeat(b, 202));
    await peer.PublishAsync(new MqttApplicationMessageBuilder().WithTopic($"amr/{a}/heartbeat").WithPayload(new byte[] { 0xff }).Build());
    await Publish(a, "heartbeat", Heartbeat(a, 299));
    await Wait(() => sink.Messages.Any(x => x.TimestampUnixMilliseconds == 299));
    Check("invalid_messages_rejected_and_recovery", validationLog.Errors.Count == 4 && sink.Messages.Count == 9,
        "Four invalid messages rejected, none reaches sink; next valid message received", new { errors = validationLog.Errors.ToArray(), delivered = sink.Messages.Count });

    var expectedCommands = new List<string>();
    foreach (var type in new[] { "MOVE", "STOP", "RESET" })
    {
        object parameters = type == "MOVE" ? new { x = 30.0, y = 15.0 } : new { };
        var payload = JsonSerializer.Serialize(new { cmdId = "audit-" + type, type, ts = 2208988800123L, param = parameters });
        expectedCommands.Add(payload);
        await manager.PublishCommandAsync(a, payload);
    }
    await Wait(() => received.Count >= 3);
    Check("command_payload_transport", received.Count == 3 && received.All(x => x.topic == $"amr/{a}/command") &&
        received.Select(x => x.payload).SequenceEqual(expectedCommands),
        "MOVE, STOP, RESET payloads reach only the chosen AMR subscription", received.Select(x => new { x.topic, x.payload }).ToArray());
    Check("command_topic_contract", received.Count == 3 && received.All(x => x.topic == $"amr/{a}/command"),
        "COM-01 verification 6 requires amr/{id}/command", received.Select(x => x.topic).ToArray());
    var before = received.Count;
    bool invalidTypeThrew = false, invalidJsonThrew = false;
    try { await manager.PublishCommandAsync(a, "{\"cmdId\":\"audit-invalid\",\"type\":\"FLY\",\"ts\":1,\"param\":{}}"); } catch (ArgumentException) { invalidTypeThrew = true; }
    try { await manager.PublishCommandAsync(a, "{"); } catch (ArgumentException) { invalidJsonThrew = true; }
    await Wait(() => received.Count >= before + 2, 600);
    Check("invalid_commands_rejected_before_publish", invalidTypeThrew && invalidJsonThrew && received.Count == before,
        "Undefined Command type and malformed JSON are rejected before MQTT publish", new { invalidTypeThrew, invalidJsonThrew, forwarded = received.Count - before });

    sink.BlockTimestamp = 401;
    await Publish(a, "heartbeat", Heartbeat(a, 401));
    await sink.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
    await Publish(a, "heartbeat", Heartbeat(a, 402));
    await Publish(b, "heartbeat", Heartbeat(b, 403));
    bool bPassed = await Wait(() => sink.Messages.Any(x => x.TimestampUnixMilliseconds == 403), 1000);
    bool aOvertook = sink.Messages.Any(x => x.TimestampUnixMilliseconds == 402);
    sink.Release.TrySetResult();
    await Wait(() => sink.Messages.Any(x => x.TimestampUnixMilliseconds == 402) && sink.Messages.Any(x => x.TimestampUnixMilliseconds == 403));
    Check("same_amr_order", !aOvertook && sink.Messages.Where(x => x.TimestampUnixMilliseconds is 401 or 402).Select(x => x.TimestampUnixMilliseconds).SequenceEqual(new long[] { 401, 402 }),
        "Second message from A waits for A's blocked first message", new { aOvertook });
    Check("other_amr_independent", bPassed, "B is delivered while A is blocked (COM-01 verification 5)", new { bDeliveredBeforeReleasingA = bPassed, observationMilliseconds = 1000 });
    collection = "completed";
}
catch (Exception ex) { error = ex.ToString(); Console.Error.WriteLine(error); }
finally
{
    sink.Release.TrySetResult();
    await File.WriteAllTextAsync(Path.Combine(output, "typed-messages.json"), JsonSerializer.Serialize(sink.Messages.Select(x => JsonSerializer.SerializeToElement(x, x.GetType())), json));
    await File.WriteAllTextAsync(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new
    {
        executed_at = DateTimeOffset.Now, collection, failed_checks = failed, error,
        environment = new { options.Host, options.Port, mqttnet = "5.2.0.1603", test_doubles = "Recording/gated Core sink and validation logger; real Mosquitto transport; synthetic AMR peer" }, checks
    }, json));
    if (peer.IsConnected) await peer.DisconnectAsync();
}
return collection == "completed" ? 0 : 1;

sealed class RecordingSink(string prefix) : IIncomingMessageSink
{
    public ConcurrentQueue<IncomingMessage> Messages { get; } = new();
    public long BlockTimestamp { get; set; } = long.MinValue;
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async ValueTask ReceiveAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        if (!message.AmrId.StartsWith(prefix, StringComparison.Ordinal)) return;
        if (message.TimestampUnixMilliseconds == BlockTimestamp) { Entered.TrySetResult(); await Release.Task; }
        Messages.Enqueue(message);
    }
}
sealed class RecordingLog(string prefix) : IValidationLog
{
    public ConcurrentQueue<ValidationFailureLog> Errors { get; } = new();
    public void Rejected(ValidationFailureLog failure)
    {
        if (failure.Topic?.Contains(prefix, StringComparison.Ordinal) == true) Errors.Enqueue(failure);
    }
}
