using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amr.Communication.Mqtt;

var json = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
try
{
    if (args.Contains("--help"))
    {
        Console.WriteLine("AMR MQTT connection host: --host HOST --port PORT --keepalive-seconds N --timeout-seconds N --duration-seconds N");
        Console.WriteLine("Defaults: localhost:1883, keepalive 5s, timeout 3s; run until Ctrl+C unless duration is provided.");
        Console.WriteLine("Environment: AMR_MQTT_HOST, AMR_MQTT_PORT, AMR_MQTT_KEEPALIVE_SECONDS, AMR_MQTT_TIMEOUT_SECONDS, AMR_MQTT_USERNAME, AMR_MQTT_PASSWORD.");
        return 0;
    }
    var arguments = new Dictionary<string, string>();
    var allowed = new HashSet<string> { "--host", "--port", "--keepalive-seconds", "--timeout-seconds", "--duration-seconds" };
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
    using var stop = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
    await using var manager = new MqttConnectionManager(options);
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
    var wait = Task.Delay(duration ?? System.Threading.Timeout.InfiniteTimeSpan, stop.Token);
    await Task.WhenAny(wait, connectionLost.Task);
    var lost = connectionLost.Task.IsCompleted;
    await manager.DisconnectAsync();
    return lost ? 3 : 0;
}
catch (Exception error)
{
    Console.Error.WriteLine(JsonSerializer.Serialize(new { Event = "HostError", Error = error.GetType().Name }));
    return 1;
}
