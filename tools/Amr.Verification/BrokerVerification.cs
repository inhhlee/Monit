using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Amr.Verification;

/// <summary>
/// 실제 Mosquitto Broker를 Docker 컨테이너로 띄워 
/// MQTT 연결, KeepAlive 유지, 강제 종료 및 응답 차단 상황 등
/// 전체 수명주기(Lifecycle) 시나리오를 통합 검증하고 결과를 기록하는 클래스입니다.
/// </summary>
public sealed class BrokerVerification
{
    const string Image = "eclipse-mosquitto:2.1.2-alpine";
    readonly string root;
    readonly string docker;
    readonly string output;
    readonly JsonObject manifest = new()
    {
        ["feature"] = "COM-03", ["spec_revision"] = 2, ["started_at"] = DateTimeOffset.Now.ToString("o"),
        ["result"] = "running", ["checks"] = new JsonObject(), ["commands"] = new JsonArray()
    };

    BrokerVerification(string root, string docker)
    {
        this.root = Path.GetFullPath(root);
        this.docker = docker;
        output = Path.Combine(this.root, "evidence", "COM-03", $"{DateTimeOffset.Now:yyyyMMddTHHmmss}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(output);
    }

    /// <summary>
    /// Docker를 이용해 Broker 통합 검증을 실행하고 결과를 JSON 매니페스트와 로그 파일로 저장합니다.
    /// </summary>
    public static async Task RunAsync(string root, string docker)
    {
        var run = new BrokerVerification(root, docker);
        try
        {
            await run.VerifyAsync();
            run.manifest["result"] = "passed";
        }
        catch (Exception error)
        {
            run.manifest["result"] = "failed";
            run.manifest["error"] = error.Message;
            throw;
        }
        finally
        {
            run.manifest["finished_at"] = DateTimeOffset.Now.ToString("o");
            await File.WriteAllTextAsync(Path.Combine(run.output, "result.json"), run.manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Evidence: {run.output}");
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }

    ProcessStartInfo CommandInfo(string executable, IEnumerable<string> args)
    {
        var info = new ProcessStartInfo(executable)
        {
            WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }

    /// <summary>
    /// 외부 프로세스(Docker, Git, Dotnet 등)를 실행하고 표준 입출력을 수집합니다.
    /// </summary>
    async Task<string> Command(string executable, string log, params string[] args)
    {
        using var process = Process.Start(CommandInfo(executable, args)) ?? throw new IOException($"Cannot start {executable}");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60)); }
        finally
        {
            if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); }
            var text = await stdout + await stderr;
            await File.WriteAllTextAsync(Path.Combine(output, log), text);
            ((JsonArray)manifest["commands"]!).Add(new JsonObject
            {
                ["at"] = DateTimeOffset.Now.ToString("o"), ["executable"] = executable,
                ["args"] = new JsonArray(args.Select(a => (JsonNode?)JsonValue.Create(a)).ToArray()),
                ["exit_code"] = process.ExitCode, ["log"] = log
            });
        }
        Require(process.ExitCode == 0, $"Command failed ({process.ExitCode}), see {log}");
        return await stdout + await stderr;
    }

    void Check(int index, string actual, params string[] artifacts)
    {
        manifest["checks"]![index.ToString()] = new JsonObject
        {
            ["result"] = "passed", ["executed_at"] = DateTimeOffset.Now.ToString("o"), ["actual"] = actual,
            ["artifacts"] = new JsonArray(artifacts.Select(a => (JsonNode?)JsonValue.Create(a)).ToArray())
        };
        Console.WriteLine($"PASS verification {index}: {actual}");
    }

    /// <summary>
    /// 전체 시나리오(연결 성공, KeepAlive 관찰, Broker 정상 종료, Broker 응답 차단, 존재하지 않는 Broker 연결 등)를
    /// 순차적으로 테스트하고 검증 증거를 수집합니다.
    /// </summary>
    async Task VerifyAsync()
    {
        var env = new JsonObject
        {
            ["os"] = Environment.OSVersion.ToString(), ["dotnet"] = Environment.Version.ToString(),
            ["broker_image"] = Image, ["mqttnet"] = "5.2.0.1603", ["protocol"] = "MQTT 5 / TCP",
            ["authentication"] = "anonymous, isolated localhost only", ["keepalive_seconds"] = 2, ["timeout_seconds"] = 3,
            ["core_amr_test_doubles"] = "none; lifecycle fake only in unit tests",
            ["response_block"] = "test TCP proxy drops broker-to-client bytes; no firewall changes"
        };
        manifest["environment"] = env;
        env["code_commit"] = (await Command("git", "code-version.log", "rev-parse", "HEAD")).Trim();
        env["changes"] = await Command("git", "code-changes.log", "status", "--short");
        env["docker"] = (await Command(docker, "docker-version.log", "version", "--format", "{{.Server.Version}}")).Trim();
        env["image_id"] = (await Command(docker, "image-id.log", "image", "inspect", Image, "--format", "{{.Id}}")).Trim();
        await Command("dotnet", "build.log", "build", "AmrControlSystem.slnx", "--no-restore");
        var hashes = new JsonObject();
        foreach (var folder in new[] { "src", "tests", "tools" })
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root, folder), "*", SearchOption.AllDirectories).Order())
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Split('/').Any(p => p is "bin" or "obj") || Path.GetExtension(file) is not (".cs" or ".csproj")) continue;
                hashes[relative] = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(file))).ToLowerInvariant();
            }
        env["source_sha256"] = hashes;
        await File.WriteAllTextAsync(Path.Combine(output, "mosquitto.conf"), "listener 1883 0.0.0.0\nallow_anonymous true\npersistence false\nlog_dest stdout\nlog_type all\n");

        await using (var broker = await Broker.Start(this, "healthy"))
        {
            await using (var host = new Host(this, "idle", broker.Port, 6))
            {
                var connected = await host.WaitState("Connected");
                Check(1, $"Connected to actual Mosquitto at 127.0.0.1:{broker.Port}", "idle-client.log", "healthy-broker.log", "mosquitto.conf");
                await host.Finish(0);
                var events = host.Events.ToArray();
                var disconnected = events.First(e => e.Data["State"]?.GetValue<string>() == "Disconnecting");
                var observed = Stopwatch.GetElapsedTime(connected.At, disconnected.At).TotalSeconds;
                Require(observed >= 4, "Idle observation shorter than 2 * KeepAlive");
                var clientId = events.First(e => e.Data["Event"]?.GetValue<string>() == "Configuration").Data["ClientId"]!.GetValue<string>();
                var log = await broker.Logs();
                Require(log.Contains("New client connected") && log.Contains(clientId), "Missing broker connection evidence");
                Require(log.Contains($"Received PINGREQ from {clientId}") && log.Contains($"Sending PINGRESP to {clientId}"), "Missing PINGREQ/PINGRESP");
                Require(!log.Contains("Received PUBLISH") && !log.Contains("Received SUBSCRIBE"), "Unexpected business traffic");
                Check(2, $"Idle maintained {observed:F3}s with PINGREQ/PINGRESP and no business messages", "idle-client.log", "healthy-broker.log");
            }
            await using var restart = new Host(this, "restart", broker.Port, 1);
            await restart.WaitState("Connected");
            await restart.Finish(0);
        }

        double shutdownDelay;
        await using (var broker = await Broker.Start(this, "shutdown"))
        await using (var host = new Host(this, "shutdown", broker.Port, 30))
        {
            await host.WaitState("Connected");
            var stopped = Stopwatch.GetTimestamp();
            await broker.Stop();
            var fault = await host.WaitState("Faulted");
            shutdownDelay = Stopwatch.GetElapsedTime(stopped, fault.At).TotalSeconds;
            Require(fault.Data["Reason"]?.GetValue<string>() == "ConnectionLost" && shutdownDelay <= 5, "Broker shutdown detection outside 5s");
            await host.Finish(3);
            Require((await broker.Logs()).Contains("terminating"), "Missing orderly Mosquitto termination log");
        }

        await using (var broker = await Broker.Start(this, "blackhole"))
        await using (var proxy = new ResponseDropProxy(broker.Port))
        {
            double delay;
            await using (var host = new Host(this, "blackhole", proxy.Port, 30))
            {
                await host.WaitState("Connected");
                var blocked = Stopwatch.GetTimestamp();
                proxy.Drop = true;
                var fault = await host.WaitState("Faulted", 8);
                delay = Stopwatch.GetElapsedTime(blocked, fault.At).TotalSeconds;
                Require(fault.Data["Reason"]?.GetValue<string>() == "ConnectionLost" && delay <= 8, "Response loss detection outside 8s");
                Require(proxy.DroppedBytes > 0 && proxy.Error is null, "Proxy did not drop responses cleanly");
                await host.Finish(3);
            }
            await using (var probe = new Host(this, "broker-still-alive", broker.Port, 1))
            {
                await probe.WaitState("Connected");
                await probe.Finish(0);
            }
            await File.WriteAllTextAsync(Path.Combine(output, "fault-timing.json"), JsonSerializer.Serialize(new
            {
                normal_shutdown_seconds = shutdownDelay, response_drop_seconds = delay, dropped_bytes = proxy.DroppedBytes,
                broker_still_alive = true, response_drop_limit_seconds = 8, normal_shutdown_limit_seconds = 5
            }));
            Check(3, $"Shutdown detected in {shutdownDelay:F3}s; blocked responses in {delay:F3}s; broker remained alive",
                "shutdown-client.log", "shutdown-broker.log", "blackhole-client.log", "blackhole-broker.log", "broker-still-alive-client.log", "fault-timing.json");
        }

        using (var unused = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        {
            unused.ExclusiveAddressUse = true;
            unused.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            await using var host = new Host(this, "unavailable", ((IPEndPoint)unused.LocalEndPoint!).Port, 1);
            var fault = await host.WaitState("Faulted");
            await host.Finish(2);
            Require(fault.Data["Reason"]?.GetValue<string>() == "ConnectFailed" && !string.IsNullOrEmpty(fault.Data["Error"]?.GetValue<string>()), "Initial failure missing reason");
            Require(!host.Events.Any(e => e.Data["State"]?.GetValue<string>() == "Connected"), "Unavailable broker reported connected");
            Require(Stopwatch.GetElapsedTime(host.Started, fault.At).TotalSeconds <= 5, "Initial failure exceeded 5s");
            Check(4, "Unavailable broker produced ConnectFailed with reason, no Connected state and controlled exit 2", "unavailable-client.log");
        }
        await Command("dotnet", "unit.log", "test", "tests/Amr.Communication.Tests/Amr.Communication.Tests.csproj", "--no-build", "--no-restore", "--logger", "trx;LogFileName=unit.trx", "--results-directory", output);
        var count = TrxValidator.Verify(Path.Combine(output, "unit.trx"));
        Check(5, $"{count} tests passed; actual Mosquitto checks 1-4 and process restart passed", "build.log", "unit.log", "unit.trx", "restart-client.log", "result.json");
    }

    /// <summary>
    /// 테스트용 Mosquitto Broker 컨테이너 인스턴스를 시작하고 종료를 관리합니다.
    /// </summary>
    sealed class Broker : IAsyncDisposable
    {
        readonly BrokerVerification run;
        readonly string label;
        readonly string name;
        bool created;
        public int Port { get; private set; }
        Broker(BrokerVerification run, string label)
        {
            this.run = run; this.label = label; name = $"amr-com03-{label}-{Guid.NewGuid():N}";
        }
        public static async Task<Broker> Start(BrokerVerification run, string label)
        {
            var broker = new Broker(run, label);
            try
            {
                // Create and start separately so cleanup knows the exact owned container after creation.
                await run.Command(run.docker, $"{label}-create.log", "create", "--name", broker.name,
                    "--label", "amr.verification=COM-03", "-p", "127.0.0.1::1883", "--mount",
                    $"type=bind,source={Path.Combine(run.output, "mosquitto.conf")},target=/mosquitto/config/mosquitto.conf,readonly", Image);
                broker.created = true;
                await run.Command(run.docker, $"{label}-start.log", "start", broker.name);
                var address = (await run.Command(run.docker, $"{label}-port.log", "port", broker.name, "1883/tcp")).Trim();
                broker.Port = int.Parse(address.Split(':')[^1]);
                var started = Stopwatch.GetTimestamp();
                while (true)
                {
                    try
                    {
                        using var client = new TcpClient();
                        await client.ConnectAsync(IPAddress.Loopback, broker.Port).WaitAsync(TimeSpan.FromSeconds(1));
                        return broker;
                    }
                    catch (Exception e) when (e is SocketException or TimeoutException)
                    {
                        if (Stopwatch.GetElapsedTime(started).TotalSeconds >= 10) throw;
                        await Task.Delay(100);
                    }
                }
            }
            catch { await broker.DisposeAsync(); throw; }
        }
        public Task<string> Logs() => run.Command(run.docker, $"{label}-broker.log", "logs", name);
        public Task<string> Stop() => run.Command(run.docker, $"{label}-stop.log", "stop", "--time", "5", name);
        public async ValueTask DisposeAsync()
        {
            if (!created) return;
            try { await Logs(); }
            finally { await run.Command(run.docker, $"{label}-cleanup.log", "rm", "-f", name); created = false; }
        }
    }

    sealed record HostEvent(long At, JsonNode Data);
    /// <summary>
    /// .NET 콘솔 애플리케이션(Amr.Communication.Console)을 하위 프로세스로 실행하고
    /// 표준 출력에서 JSON 형식의 상태 이벤트를 파싱하여 추적합니다.
    /// </summary>
    sealed class Host : IAsyncDisposable
    {
        readonly BrokerVerification run;
        readonly string label;
        readonly Process process;
        readonly Task reader;
        readonly Task<string> stderr;
        public readonly ConcurrentQueue<HostEvent> Events = new();
        public long Started { get; } = Stopwatch.GetTimestamp();
        public Host(BrokerVerification run, string label, int port, int duration)
        {
            this.run = run; this.label = label;
            var info = run.CommandInfo("dotnet", new[]
            {
                Path.Combine(run.root, "src/Amr.Communication.Console/bin/Debug/net10.0/Amr.Communication.Console.dll"),
                "--host", "127.0.0.1", "--port", port.ToString(), "--keepalive-seconds", "2", "--timeout-seconds", "3", "--duration-seconds", duration.ToString()
            });
            info.Environment.Remove("AMR_MQTT_USERNAME");
            info.Environment.Remove("AMR_MQTT_PASSWORD");
            process = Process.Start(info) ?? throw new IOException("Cannot start console");
            reader = Read();
            stderr = process.StandardError.ReadToEndAsync();
        }
        async Task Read()
        {
            await using var log = new StreamWriter(Path.Combine(run.output, $"{label}-client.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                await log.WriteLineAsync(line);
                try { if (JsonNode.Parse(line) is { } node) Events.Enqueue(new(Stopwatch.GetTimestamp(), node)); }
                catch (JsonException) { }
            }
        }
        public async Task<HostEvent> WaitState(string state, int timeout = 5)
        {
            var start = Stopwatch.GetTimestamp();
            while (Stopwatch.GetElapsedTime(start).TotalSeconds < timeout)
            {
                if (process.HasExited) await reader;
                var found = Events.FirstOrDefault(e => e.Data["State"]?.GetValue<string>() == state);
                if (found is not null) return found;
                if (process.HasExited) break;
                await Task.Delay(20);
            }
            throw new InvalidDataException($"{label}: expected state {state}; see client log");
        }
        public async Task Finish(int expected)
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await reader;
            Require(process.ExitCode == expected, $"{label}: expected exit {expected}, got {process.ExitCode}");
        }
        public async ValueTask DisposeAsync()
        {
            if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); }
            await reader;
            await File.AppendAllTextAsync(Path.Combine(run.output, $"{label}-client.log"), await stderr);
            ((JsonArray)run.manifest["commands"]!).Add(new JsonObject
            {
                ["at"] = DateTimeOffset.Now.ToString("o"), ["executable"] = "dotnet",
                ["args"] = new JsonArray(process.StartInfo.ArgumentList.Select(a => (JsonNode?)JsonValue.Create(a)).ToArray()),
                ["exit_code"] = process.ExitCode, ["log"] = $"{label}-client.log"
            });
            process.Dispose();
        }
    }
}
