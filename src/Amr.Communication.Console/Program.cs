using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amr.Communication.Mqtt;
using Amr.Communication.Models;
using Amr.Communication.Validation;

var json = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
try
{
    if (args.Contains("--help"))
    {
        Console.WriteLine("AMR MQTT connection host: --host HOST --port PORT --keepalive-seconds N --timeout-seconds N --duration-seconds N --heartbeat");
        Console.WriteLine("One command: --command MOVE --amr-id AMR_001 --x 30 --y 15 (STOP/RESET need no coordinates). ACK reception is enabled automatically.");
        Console.WriteLine("Defaults: localhost:1883, keepalive 5s, timeout 3s; run until Ctrl+C unless duration is provided.");
        Console.WriteLine("Environment: AMR_MQTT_HOST, AMR_MQTT_PORT, AMR_MQTT_KEEPALIVE_SECONDS, AMR_MQTT_TIMEOUT_SECONDS, AMR_MQTT_USERNAME, AMR_MQTT_PASSWORD.");
        return 0;
    }
    // 직접 실행은 기존 연결 전용 모드, --heartbeat를 주면 수신 실습을 켭니다.
    if (args.Count(arg => arg == "--heartbeat") > 1)
        throw new ArgumentException("Duplicate --heartbeat argument.");
    var receiveHeartbeats = args.Contains("--heartbeat");
    args = args.Where(arg => arg != "--heartbeat").ToArray();
    var arguments = new Dictionary<string, string>();
    var allowed = new HashSet<string> { "--host", "--port", "--keepalive-seconds", "--timeout-seconds", "--duration-seconds", "--command", "--amr-id", "--x", "--y" };
    for (var i = 0; i < args.Length; i += 2)
    {
        if (!allowed.Contains(args[i]) || i + 1 >= args.Length || !arguments.TryAdd(args[i], args[i + 1]))
            throw new ArgumentException("Unknown, duplicate or missing argument; use --help.");
    }
    string Value(string name, string environment, string fallback) =>
        arguments.GetValueOrDefault(name) ?? Environment.GetEnvironmentVariable(environment) ?? fallback;
    double Number(string value) => double.Parse(value, CultureInfo.InvariantCulture);
    var options = new BrokerConnectionOptions
    {
        Host = Value("--host", "AMR_MQTT_HOST", "127.0.0.1"),
        Port = int.Parse(Value("--port", "AMR_MQTT_PORT", "1883"), CultureInfo.InvariantCulture),
        KeepAlive = TimeSpan.FromSeconds(Number(Value("--keepalive-seconds", "AMR_MQTT_KEEPALIVE_SECONDS", "5"))),
        Timeout = TimeSpan.FromSeconds(Number(Value("--timeout-seconds", "AMR_MQTT_TIMEOUT_SECONDS", "3"))),
        Username = Environment.GetEnvironmentVariable("AMR_MQTT_USERNAME"),
        Password = Environment.GetEnvironmentVariable("AMR_MQTT_PASSWORD")
    };
    TimeSpan? duration = arguments.TryGetValue("--duration-seconds", out var value) ? TimeSpan.FromSeconds(Number(value)) : null;
    if (duration.HasValue && (duration.Value <= TimeSpan.Zero || duration.Value > TimeSpan.FromDays(1)))
        throw new ArgumentException("Duration must be positive and at most one day.");
    string? commandPayload = null;
    var targetAmr = arguments.GetValueOrDefault("--amr-id", "AMR_001");
    if (arguments.TryGetValue("--command", out var commandType))
    {
        object parameters;
        if (commandType == "MOVE")
        {
            if (!arguments.TryGetValue("--x", out var x) || !arguments.TryGetValue("--y", out var y))
                throw new ArgumentException("MOVE requires --x and --y.");
            parameters = new { x = Number(x), y = Number(y) };
        }
        else
        {
            if (arguments.ContainsKey("--x") || arguments.ContainsKey("--y"))
                throw new ArgumentException("Coordinates are only valid for MOVE.");
            parameters = new { };
        }
        // 콘솔은 Core의 요청 생성 대역입니다. 통신 계층은 이 ID를 변경하지 않습니다.
        commandPayload = JsonSerializer.Serialize(new
        {
            cmdId = $"CMD-{Guid.NewGuid():N}", type = commandType,
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), param = parameters
        });
        var validation = new CommandValidator().Validate(System.Text.Encoding.UTF8.GetBytes(commandPayload));
        if (!validation.IsValid) throw new ArgumentException(validation.Error!.Reason);
        _ = MqttTopics.Target.Command(targetAmr);
    }
    else if (arguments.Keys.Any(key => key is "--amr-id" or "--x" or "--y"))
        throw new ArgumentException("--amr-id/--x/--y require --command.");
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
    var processor = new IncomingMessageProcessor(new ConsoleMessageSink(), new ConsoleValidationLog());
    var receiveMessages = receiveHeartbeats || commandPayload is not null;
    await using var manager = receiveMessages
        ? new MqttConnectionManager(options, processor)
        : new MqttConnectionManager(options);
    var connectionLost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    manager.StateChanged += snapshot =>
    {
        Console.WriteLine(JsonSerializer.Serialize(snapshot, json));
        if (snapshot.Reason == "ConnectionLost") connectionLost.TrySetResult();
    };
    Console.WriteLine(JsonSerializer.Serialize(new { Event = "Configuration", options.Host, options.Port,
        options.ClientId, KeepAliveSeconds = options.KeepAlive.TotalSeconds, TimeoutSeconds = options.Timeout.TotalSeconds,
        Authentication = options.Username is null ? "anonymous" : "configured" }));
    if (!await manager.ConnectAsync(stop.Token)) return stop.IsCancellationRequested ? 0 : 2;
    if (receiveMessages)
        Console.WriteLine("Subscribed: amr/+/heartbeat, amr/+/telemetry, amr/+/alarm, amr/+/ack");
    if (commandPayload is not null)
    {
        await manager.PublishCommandAsync(targetAmr, commandPayload, stop.Token);
        Console.WriteLine($"Published Command to {targetAmr}: {commandPayload}");
    }
    var wait = Task.Delay(duration ?? System.Threading.Timeout.InfiniteTimeSpan, stop.Token);
    await Task.WhenAny(wait, connectionLost.Task);
    var lost = connectionLost.Task.IsCompleted;
    await manager.DisconnectAsync();
    return lost ? 3 : 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(new { Event = "HostError", Error = error.GetType().Name,
        Reason = error is ArgumentException ? error.Message : "Host operation failed." }));
    return 1;
}

// 실제 Core 대신, 검사를 통과한 메시지를 콘솔에서 확인하는 실습용 수신자입니다.
sealed class ConsoleMessageSink : IIncomingMessageSink
{
    public ValueTask ReceiveAsync(IncomingMessage message, CancellationToken cancellationToken)
    {
        if (message is AckMessage ack)
            Console.WriteLine($"Received Ack: {ack.AmrId} " + JsonSerializer.Serialize(new
            {
                cmdId = ack.CommandId, result = ack.Result.ToString().ToUpperInvariant(),
                reason = ack.Reason, ts = ack.TimestampUnixMilliseconds
            }));
        else
            Console.WriteLine($"Received {message.Kind}: {message.AmrId} ts={message.TimestampUnixMilliseconds}");
        return ValueTask.CompletedTask;
    }
}

sealed class ConsoleValidationLog : IValidationLog
{
    public void Rejected(ValidationFailureLog failure)
        => Console.WriteLine(JsonSerializer.Serialize(new
        {
            Event = "Rejected", failure.Topic, failure.Error.Code, failure.Error.Field, failure.Error.Reason
        }));
}
