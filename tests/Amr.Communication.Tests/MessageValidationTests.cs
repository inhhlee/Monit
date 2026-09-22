using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Amr.Communication.Models;
using Amr.Communication.Validation;
using Xunit.Abstractions;

namespace Amr.Communication.Tests;

public class MessageValidationTests(ITestOutputHelper output)
{
    private const long FutureTimestamp = 2_208_988_800_123L;
    private static readonly string[] Kinds = ["telemetry", "alarm", "heartbeat", "ack"];

    /// <summary>
    /// 정상객체 정의
    /// </summary>    
    private static string Sample(string kind) => kind switch
    {
        "telemetry" => """{"id":"AMR_001","ts":2208988800123,"Pos":{"x":1.25,"y":-2.5,"sp":0.75,"stat":"IDLE"}}""",
        "alarm" => """{"id":"AMR_001","ts":2208988800123,"code":"A001","level":"WARNING","message":"장애물 감지"}""",
        "heartbeat" => """{"id":"AMR_001","ts":2208988800123}""",
        "ack" => """{"cmdId":"cmd-001","result":"SUCCESS","ts":2208988800123}""",
        _ => throw new ArgumentException(kind)
    };

    /// <summary>
    /// 각 topic에 payload에 대한 필수 필드.
    /// </summary>    
    private static string[] Fields(string kind) => kind switch
    {
        "telemetry" => ["id", "ts", "Pos", "Pos.x", "Pos.y", "Pos.sp", "Pos.stat"],
        "alarm" => ["id", "ts", "code", "level", "message"],
        "heartbeat" => ["id", "ts"],
        "ack" => ["cmdId", "result", "ts"],
        _ => throw new ArgumentException(kind)
    };

/// <summary>
/// 정상적인 json payload를 변경하여, 필드가 없거나 잘못된 타입으로 변경한 payload를 생성한다.
/// </summary>
    private static string Change(string kind, string path, string? rawValue)
    {
        var node = JsonNode.Parse(Sample(kind))!.AsObject();
        var parts = path.Split('.');
        var parent = parts.Length == 1 ? node : node[parts[0]]!.AsObject();
        if (rawValue is null) parent.Remove(parts[^1]);
        else parent[parts[^1]] = JsonNode.Parse(rawValue);
        return node.ToJsonString();
    }

/// <summary>
/// 정상객체 정의 
/// </summary>
    private static IncomingMessage Expected(string kind) => kind switch
    {
        "telemetry" => new TelemetryMessage("AMR_001", FutureTimestamp, new(1.25, -2.5, 0.75, AmrState.Idle)),
        "alarm" => new AlarmMessage("AMR_001", FutureTimestamp, "A001", AlarmLevel.Warning, "장애물 감지"),
        "heartbeat" => new HeartbeatMessage("AMR_001", FutureTimestamp),
        "ack" => new AckMessage("AMR_001", FutureTimestamp, "cmd-001", AckResult.Success),
        _ => throw new ArgumentException(kind)
    };

    [Theory]
    [InlineData("telemetry", IncomingMessageKind.Telemetry)]
    [InlineData("alarm", IncomingMessageKind.Alarm)]
    [InlineData("heartbeat", IncomingMessageKind.Heartbeat)]
    [InlineData("ack", IncomingMessageKind.Ack)]
    public async Task NormalMessagesPreserveAllFieldsAtCoreBoundary(string kind, IncomingMessageKind expectedKind)
    {
        var probe = new Probe();
        var result = await Run(probe, $"amr/AMR_001/{kind}", Sample(kind));
        Assert.True(result.IsValid);
        Assert.Null(result.Error);
        Assert.Equal(Expected(kind), Assert.Single(probe.Messages));
        Assert.Equal(expectedKind, result.Message!.Kind);
        Assert.Empty(probe.Failures);
    }

    public static IEnumerable<object[]> MissingFields() =>
        Kinds.SelectMany(kind => Fields(kind).Select(field => new object[] { kind, field }));

    [Theory]
    [MemberData(nameof(MissingFields))]
    public async Task EveryRequiredFieldMustBePresent(string kind, string field)
        => await Reject($"amr/AMR_001/{kind}", Change(kind, field, null), "MissingField", "$." + field);

    public static IEnumerable<object[]> WrongTypes()
    {
        foreach (var kind in Kinds)
        foreach (var field in Fields(kind))
        {
            var type = field == "Pos" ? "object" : field == "ts" || field is "Pos.x" or "Pos.y" or "Pos.sp" ? "number" : "string";
            foreach (var (value, valueType) in new[] { ("null", "null"), ("true", "boolean"), ("[]", "array"), ("{}", "object"), ("123", "number"), ("\"text\"", "string") })
                if (type != valueType) yield return [kind, field, value];
        }
    }

    [Theory]
    [MemberData(nameof(WrongTypes))]
    public async Task EveryRequiredFieldRejectsWrongJsonTypes(string kind, string field, string value)
        => await Reject($"amr/AMR_001/{kind}", Change(kind, field, value), "InvalidType", "$." + field);

    [Theory]
    [InlineData("", "EmptyPayload")]
    [InlineData("   ", "InvalidJson")]
    [InlineData("{", "InvalidJson")]
    [InlineData("{\"id\":", "InvalidJson")]
    [InlineData("null", "InvalidType")]
    [InlineData("[]", "InvalidType")]
    [InlineData("123", "InvalidType")]
    [InlineData("true", "InvalidType")]
    [InlineData("\"text\"", "InvalidType")]
    [InlineData("{\"id\":\"AMR_001\",\"ts\":1,}", "InvalidJson")]
    [InlineData("{/*comment*/\"id\":\"AMR_001\",\"ts\":1}", "InvalidJson")]
    [InlineData("{} {}", "InvalidJson")]
    public async Task InvalidJsonAndRootShapesDoNotReachCore(string json, string code)
        => await Reject("amr/AMR_001/heartbeat", json, code, "$");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("amr//heartbeat")]
    [InlineData("amr/AMR 001/heartbeat")]
    [InlineData("amr/AMR\n001/heartbeat")]
    [InlineData("amr/+/heartbeat")]
    [InlineData("amr/AMR#/heartbeat")]
    [InlineData("AMR/AMR_001/heartbeat")]
    [InlineData("amr/AMR_001/Heartbeat")]
    [InlineData("amr/AMR_001/state")]
    [InlineData("amr/AMR_001/cmd/ack")]
    [InlineData("amr/AMR_001/command")]
    [InlineData("amr/AMR_001/heartbeat/")]
    [InlineData("/amr/AMR_001/heartbeat")]
    public async Task UnsupportedTopicsAreRejected(string? topic)
        => await Reject(topic, Sample("heartbeat"), "UnsupportedTopic", "$topic");

    public static IEnumerable<object[]> MismatchedKinds() => Kinds.SelectMany(topicKind =>
        Kinds.Where(payloadKind => topicKind != payloadKind).Select(payloadKind => new object[] { topicKind, payloadKind }));

    [Theory]
    [MemberData(nameof(MismatchedKinds))]
    // 데이터 일관성 확인 heartbeat면 heartbeat payload만 허용, telemetry면 telemetry payload만 허용
    public async Task PayloadForDifferentTopicKindIsRejected(string topicKind, string payloadKind)
        => await Reject($"amr/AMR_001/{topicKind}", Sample(payloadKind));


    [Theory]
    [InlineData("telemetry")]
    [InlineData("alarm")]
    [InlineData("heartbeat")]
    [InlineData("ack")]
    // 토픽과 페이로드의 id가 일치해야 한다. 토픽의 id와 페이로드의 id가 다르면 reject
    public async Task PayloadIdWhenPresentMustMatchTopic(string kind)
        => await Reject($"amr/AMR_001/{kind}", Change(kind, "id", "\"AMR_002\""), "IdMismatch", "$.id");

    [Theory]
    [InlineData("null", "InvalidType")]
    [InlineData("1", "InvalidType")]
    [InlineData("\"\"", "InvalidValue")]
    public async Task OptionalAckIdIsValidatedWhenPresent(string id, string code)
        => await Reject("amr/AMR_001/ack", Change("ack", "id", id), code, "$.id");

    [Fact]
    public async Task OptionalAckIdAndValidFailureResultArePreserved()
    {
        var probe = new Probe();
        var json = Change("ack", "id", "\"AMR_001\"").Replace("SUCCESS", "FAILURE");
        var result = await Run(probe, "amr/AMR_001/ack", json);
        Assert.True(result.IsValid);
        Assert.Equal(new AckMessage("AMR_001", FutureTimestamp, "cmd-001", AckResult.Failure), Assert.Single(probe.Messages));
        Assert.Empty(probe.Failures);
    }

    [Theory]
    [InlineData("telemetry", "Pos.stat", "\"FLYING\"")]
    [InlineData("telemetry", "Pos.stat", "\"idle\"")]
    [InlineData("alarm", "level", "\"FATAL\"")]
    [InlineData("alarm", "level", "\"warning\"")]
    [InlineData("ack", "result", "\"UNKNOWN\"")]
    [InlineData("ack", "result", "\"success\"")]
    [InlineData("telemetry", "Pos.sp", "-0.01")]
    [InlineData("telemetry", "Pos.x", "1e400")]
    [InlineData("telemetry", "Pos.y", "-1e400")]
    [InlineData("telemetry", "Pos.sp", "1e400")]
    /// 필드에 대한 enum과 숫자 범위 검증
    public async Task InvalidEnumsAndNumericRangesAreRejected(string kind, string field, string value)
        => await Reject($"amr/AMR_001/{kind}", Change(kind, field, value), "OutOfRange", "$." + field);

    [Theory]
    [InlineData("telemetry", "Pos.stat", "IDLE", "Idle")]
    [InlineData("telemetry", "Pos.stat", "MOVING", "Moving")]
    [InlineData("telemetry", "Pos.stat", "CHARGING", "Charging")]
    [InlineData("telemetry", "Pos.stat", "ERROR", "Error")]
    [InlineData("alarm", "level", "INFO", "Info")]
    [InlineData("alarm", "level", "WARNING", "Warning")]
    [InlineData("alarm", "level", "ERROR", "Error")]
    [InlineData("ack", "result", "SUCCESS", "Success")]
    [InlineData("ack", "result", "FAILURE", "Failure")]
    public async Task EveryAllowedEnumValueReachesCore(string kind, string field, string value, string expected)
    {
        var probe = new Probe();
        await Run(probe, $"amr/AMR_001/{kind}", Change(kind, field, JsonSerializer.Serialize(value)));
        var actual = Assert.Single(probe.Messages) switch
        {
            TelemetryMessage t => t.Position.State.ToString(),
            AlarmMessage a => a.Level.ToString(),
            AckMessage a => a.Result.ToString(),
            _ => throw new InvalidOperationException()
        };
        Assert.Equal(expected, actual);
        Assert.Empty(probe.Failures);
    }

    public static IEnumerable<object[]> TimestampBoundaries() => Kinds.SelectMany(kind =>
        new[] { long.MinValue, -1L, 0L, FutureTimestamp, long.MaxValue }.Select(ts => new object[] { kind, ts }));

    [Theory]
    [MemberData(nameof(TimestampBoundaries))]
    public async Task Int64TimestampsArePreservedWithoutDateConversion(string kind, long timestamp)
    {
        var probe = new Probe();
        await Run(probe, $"amr/AMR_001/{kind}", Change(kind, "ts", timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(timestamp, Assert.Single(probe.Messages).TimestampUnixMilliseconds);
        Assert.Empty(probe.Failures);
    }

    public static IEnumerable<object[]> InvalidTimestamps() => Kinds.SelectMany(kind =>
        new[] { "9223372036854775808", "-9223372036854775809", "1.5", "1.0", "1e3" }.Select(ts => new object[] { kind, ts }));

    [Theory]
    [MemberData(nameof(InvalidTimestamps))]
    public async Task OverflowFractionalAndExponentTimestampsAreRejected(string kind, string timestamp)
        => await Reject($"amr/AMR_001/{kind}", Sample(kind).Replace(FutureTimestamp.ToString(), timestamp), "InvalidType", "$.ts");

    public static IEnumerable<object[]> BlankStrings() => Kinds.SelectMany(kind =>
        Fields(kind).Where(f => f is "id" or "cmdId" or "code" or "message" or "Pos.stat" or "level" or "result")
            .SelectMany(field => new[] { "\"\"", "\"   \"" }.Select(v => new object[] { kind, field, v })));

    [Theory]
    [MemberData(nameof(BlankStrings))]
    /// 필수 문자열 필드가 비어있거나 공백이면 reject
    public async Task BlankRequiredStringsAreRejected(string kind, string field, string value)
        => await Reject($"amr/AMR_001/{kind}", Change(kind, field, value), "InvalidValue", "$." + field);

    [Theory]
    [InlineData("heartbeat", "{\"id\":\"AMR_001\",\"id\":\"AMR_001\",\"ts\":1}", "DuplicateField", "$")]
    [InlineData("heartbeat", "{\"id\":\"AMR_001\",\"ts\":1,\"t\\u0073\":2}", "DuplicateField", "$")]
    [InlineData("heartbeat", "{\"id\":\"AMR_001\",\"ts\":1,\"type\":\"heartbeat\"}", "UnexpectedField", "$")]
    [InlineData("heartbeat", "{\"ID\":\"AMR_001\",\"ts\":1}", "UnexpectedField", "$")]
    [InlineData("telemetry", "{\"id\":\"AMR_001\",\"ts\":1,\"Pos\":{\"x\":1,\"x\":2,\"y\":0,\"sp\":0,\"stat\":\"IDLE\"}}", "DuplicateField", "$.Pos")]
    [InlineData("telemetry", "{\"id\":\"AMR_001\",\"ts\":1,\"Pos\":{\"x\":1,\"y\":0,\"sp\":0,\"stat\":\"IDLE\",\"extra\":1}}", "UnexpectedField", "$.Pos")]
    /// <summary>dsf
    public async Task AmbiguousAndUnknownFieldsAreRejected(string kind, string json, string code, string path)
        => await Reject($"amr/AMR_001/{kind}", json, code, path);

    [Fact]
    public async Task InvalidUtf8IsRejectedAndNextMessageWorks()
    {
        var probe = new Probe();
        var processor = new IncomingMessageProcessor(probe, probe);
        var bytes = Encoding.UTF8.GetBytes(Sample("heartbeat"));
        bytes[bytes.AsSpan().IndexOf((byte)'A')] = 0xff;
        var result = await processor.ProcessAsync("amr/AMR_001/heartbeat", bytes);
        output.WriteLine(JsonSerializer.Serialize(new { bytes = Convert.ToHexString(bytes), result.Error }));
        Assert.Equal("InvalidUtf8", result.Error!.Code);
        Assert.Empty(probe.Messages);
        Assert.Single(probe.Failures);
        Assert.True((await processor.ProcessAsync("amr/AMR_001/heartbeat", Encoding.UTF8.GetBytes(Sample("heartbeat")))).IsValid);
        Assert.Single(probe.Messages);
    }

    // 고정 데이터 테스트 
    [Theory]
    [InlineData("{\"cmdId\":\"\\uD800\",\"result\":\"SUCCESS\",\"ts\":1}")]
    [InlineData("{\"cmdId\":\"\\uDC00\",\"result\":\"SUCCESS\",\"ts\":1}")]
    [InlineData("{\"cmdId\":\"a\",\"result\":\"SUCCESS\",\"ts\":1,\"\\uD800\":0}")]
    public async Task UnpairedUnicodeEscapesAreRejectedWithoutEscapingExceptions(string json)
        => await Reject("amr/AMR_001/ack", json, "InvalidJson");

    // 단일 테스트
    [Fact]
    // 유효한 유니코드 대리 쌍이 유지되는지 확인
    public async Task ValidUnicodeSurrogatePairIsPreserved()
    {
        var probe = new Probe();
        await Run(probe, "amr/AMR_001/ack", """{"cmdId":"\uD83D\uDE80","result":"SUCCESS","ts":1}""");
        Assert.Equal("🚀", Assert.IsType<AckMessage>(Assert.Single(probe.Messages)).CommandId);
    }

    [Fact]
    public async Task CoreAndLoggerFailuresPropagateWithoutMisclassifyingInput()
    {
        var receiver = new ThrowingReceiver();
        var processor = new IncomingMessageProcessor(receiver, receiver);
        await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync("amr/AMR_001/heartbeat",
            Encoding.UTF8.GetBytes(Sample("heartbeat"))).AsTask());
        Assert.Equal(1, receiver.CoreCalls);
        Assert.Equal(0, receiver.LogCalls);
        await Assert.ThrowsAsync<IOException>(() => processor.ProcessAsync("amr/AMR_001/heartbeat", Array.Empty<byte>()).AsTask());
        Assert.Equal(1, receiver.CoreCalls);
        Assert.Equal(1, receiver.LogCalls);
    }

    [Fact]
    public async Task PayloadLimitIncludesExactBoundaryAndRejectsOversizeAndExcessiveDepth()
    {
        var json = Sample("heartbeat");
        var probe = new Probe();
        Assert.True((await Run(probe, "amr/AMR_001/heartbeat", json.PadRight(65_536))).IsValid);
        Assert.Single(probe.Messages);
        await Reject("amr/AMR_001/heartbeat", json.PadRight(65_537), "PayloadTooLarge", "$");
        await Reject("amr/AMR_001/heartbeat", new string('[', 17) + "0" + new string(']', 17), "InvalidJson", "$");
    }

    [Fact]
    public async Task UnicodeIdentifierAndMessageContentArePreserved()
    {
        var probe = new Probe();
        await Run(probe, "amr/로봇-가/alarm", Sample("alarm").Replace("AMR_001", "로봇-가"));
        var alarm = Assert.IsType<AlarmMessage>(Assert.Single(probe.Messages));
        Assert.Equal("로봇-가", alarm.AmrId);
        Assert.Equal("장애물 감지", alarm.Message);
    }

    [Fact]
    // CancellationToken이 취소되면 메시지가 전달되지 않고 검증 실패로 잘못 분류되지 않는다.
    public async Task CancellationDoesNotDeliverOrMisclassifyAsValidationFailure()
    {
        var probe = new Probe();
        var processor = new IncomingMessageProcessor(probe, probe);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processor.ProcessAsync("amr/AMR_001/heartbeat",
            Encoding.UTF8.GetBytes(Sample("heartbeat")), new CancellationToken(true)).AsTask());
        Assert.Empty(probe.Messages);
        Assert.Empty(probe.Failures);
    }

    private async Task Reject(string? topic, string json, string? code = null, string? field = null)
    {
        var probe = new Probe();
        var processor = new IncomingMessageProcessor(probe, probe);
        var result = await processor.ProcessAsync(topic, Encoding.UTF8.GetBytes(json));
        Log(topic, json, result, probe);
        Assert.False(result.IsValid);
        Assert.Null(result.Message);
        Assert.Empty(probe.Messages);
        var failure = Assert.Single(probe.Failures);
        Assert.Equal(topic, failure.Topic);
        Assert.Same(result.Error, failure.Error);
        Assert.False(string.IsNullOrWhiteSpace(failure.Error.Reason));
        if (code is not null) Assert.Equal(code, failure.Error.Code);
        if (field is not null) Assert.Equal(field, failure.Error.Field);
        // Every rejected fixture is immediately followed by a known-good message on the same processor.
        var recovery = await processor.ProcessAsync("amr/AMR_001/heartbeat", Encoding.UTF8.GetBytes(Sample("heartbeat")));
        Assert.True(recovery.IsValid);
        Assert.Equal(Expected("heartbeat"), Assert.Single(probe.Messages));
        Assert.Single(probe.Failures);
        output.WriteLine("Recovery: same processor, next valid heartbeat delivered once; no additional error log.");
    }

/// <summary>
/// 
/// </summary>
    private async Task<ValidationResult> Run(Probe probe, string topic, string json)
    {
        var result = await new IncomingMessageProcessor(probe, probe).ProcessAsync(topic, Encoding.UTF8.GetBytes(json));
        Log(topic, json, result, probe);
        return result;
    }

    private void Log(string? topic, string json, ValidationResult result, Probe probe) =>
        output.WriteLine(JsonSerializer.Serialize(new { topic, payload = json.Length < 2048 ? json : $"{json.Length} characters (size boundary fixture)",
            result.IsValid, result.Error, coreCalls = probe.Messages.Count, failures = probe.Failures,
            delivered = probe.Messages.Select(m => JsonSerializer.Serialize(m, m.GetType())) }));

/// <summary>
/// 정상처리된 메시지와 검증 실패를 기록하기 위한 Probe 구현체이다.
/// </summary>
    private sealed class Probe : IIncomingMessageSink, IValidationLog
    {
        // list형태로 메시지와 실패를 기록한다.
        public List<IncomingMessage> Messages { get; } = [];
        public List<ValidationFailureLog> Failures { get; } = [];

        // 메시지 생성시 core로 전달.         
        public ValueTask ReceiveAsync(IncomingMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return ValueTask.CompletedTask;
        }

        // 검증 실패시 log로 전달.
        public void Rejected(ValidationFailureLog failure) => Failures.Add(failure);
    }


/// <summary>
/// 무조건 exception을 발생시키는 Probe 구현체이다. Core와 Log의 장애를 시뮬레이션한다.
/// </summary>
    private sealed class ThrowingReceiver : IIncomingMessageSink, IValidationLog
    {
        public int CoreCalls { get; private set; }
        public int LogCalls { get; private set; }
        public ValueTask ReceiveAsync(IncomingMessage message, CancellationToken cancellationToken)
        {
            CoreCalls++;
            throw new IOException("Core unavailable");
        }
        public void Rejected(ValidationFailureLog failure)
        {
            LogCalls++;
            throw new IOException("Log unavailable");
        }
    }
}
