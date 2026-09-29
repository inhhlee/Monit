using System.Text;
using System.Text.Json.Nodes;
using Amr.Communication.Models;
using Amr.Communication.Mqtt;
using Amr.Communication.Validation;
using Amr.TestClient;
using MQTTnet;

namespace Amr.Communication.Tests;

public class CommandValidationTests
{
    private const string Move = """{"cmdId":"CMD-42","type":"MOVE","ts":2208988800123,"param":{"x":30.0,"y":15.0}}""";
    private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
    private static CommandValidationResult Validate(string text) => new CommandValidator().Validate(Bytes(text));

    [Theory]
    [InlineData("MOVE", CommandType.Move)]
    [InlineData("STOP", CommandType.Stop)]
    [InlineData("RESET", CommandType.Reset)]
    public void DefinedCommandsPreserveIdTimestampAndParameters(string type, CommandType expected)
    {
        var node = JsonNode.Parse(Move)!;
        node["type"] = type;
        if (type != "MOVE") node["param"] = new JsonObject();
        var result = Validate(node.ToJsonString());
        Assert.True(result.IsValid);
        Assert.Equal("CMD-42", result.Message!.CommandId);
        Assert.Equal(2208988800123, result.Message.TimestampUnixMilliseconds);
        Assert.Equal(expected, result.Message.Type);
        Assert.Equal(type == "MOVE" ? new CommandPosition(30, 15) : null, result.Message.Position);
    }

    public static IEnumerable<object[]> InvalidFields()
    {
        foreach (var name in new[] { "cmdId", "type", "ts", "param", "param.x", "param.y" })
        {
            var parts = name.Split('.');
            foreach (var raw in new string?[] { null, "null", "true", "[]" })
            {
                var node = JsonNode.Parse(Move)!.AsObject();
                var parent = parts.Length == 1 ? node : node["param"]!.AsObject();
                if (raw is null) parent.Remove(parts[^1]);
                else parent[parts[^1]] = JsonNode.Parse(raw);
                yield return [node.ToJsonString()];
            }
        }
        foreach (var raw in new[] { "\"FLY\"", "\"move\"", "\"\"" })
        {
            var node = JsonNode.Parse(Move)!; node["type"] = JsonNode.Parse(raw);
            yield return [node.ToJsonString()];
        }
        yield return [Move.Replace("2208988800123", "9223372036854775808")];
        yield return [Move.Replace("2208988800123", "1.0")];
        yield return [Move.Replace("30.0", "1e999")];
        yield return [Move.Replace("\"MOVE\"", "\"STOP\"")];
        yield return [Move.Replace("\"type\":", "\"extra\":0,\"type\":")];
        yield return [Move.Replace("\"ts\":", "\"ts\":1,\"ts\":")];
    }

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void InvalidCommandsAreRejected(string payload)
    {
        var result = Validate(payload);
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"cmdId\":null}")]
    [InlineData("{\"cmdId\":\" \"}")]
    [InlineData("{\"cmdId\":\"a\",\"cmdId\":\"b\"}")]
    [InlineData("{\"cmdId\":\"a\",\"cmdId\":\"a\"}")]
    [InlineData("{\"cmdId\":\"\\uD800\"}")]
    public void UnusableOrAmbiguousIdsNeverProduceFabricatedAcks(string payload)
    {
        var response = BasicCommandHandler.Handle("AMR_001", Bytes(payload), 42);
        Assert.Null(response.AckPayload);
        Assert.NotNull(response.Error);
    }

    [Fact]
    public void InvalidUtf8AndOversizedInputsAreRejected()
    {
        foreach (var bytes in new[] { new byte[] { 0xff }, new byte[65_537] })
        {
            var result = new CommandValidator().Validate(bytes);
            Assert.False(result.IsValid);
            Assert.Null(result.CommandId);
            Assert.Null(BasicCommandHandler.Handle("AMR_001", bytes, 1).AckPayload);
        }
    }

    [Theory]
    [InlineData("MOVE", "SIMULATION_NOT_IMPLEMENTED")]
    [InlineData("STOP", "SIMULATION_NOT_IMPLEMENTED")]
    [InlineData("RESET", "SIMULATION_NOT_IMPLEMENTED")]
    [InlineData("FLY", "OutOfRange")]
    public void AmrReturnsSameIdAndTruthfulFailureThatPassesIncomingValidation(string type, string reason)
    {
        var node = JsonNode.Parse(Move)!; node["type"] = type;
        if (type is "STOP" or "RESET") node["param"] = new JsonObject();
        var response = BasicCommandHandler.Handle("AMR_001", Bytes(node.ToJsonString()), 123);
        Assert.NotNull(response.AckPayload);
        var received = new MessageValidator().Validate("amr/AMR_001/ack", Bytes(response.AckPayload));
        var ack = Assert.IsType<AckMessage>(received.Message);
        Assert.Equal("CMD-42", ack.CommandId);
        Assert.Equal(AckResult.Failure, ack.Result);
        Assert.Equal(123, ack.TimestampUnixMilliseconds);
        Assert.Contains(reason, ack.Reason);
    }

    [Theory]
    [InlineData("SUCCESS")]
    [InlineData("FAILURE")]
    public void OptionalAckReasonIsPreservedAndLegacyAcksStillWork(string result)
    {
        var json = "{\"cmdId\":\"C1\",\"ts\":1,\"result\":\"" + result + "\"}";
        var legacy = new MessageValidator().Validate("amr/A/ack", Bytes(json));
        Assert.Null(Assert.IsType<AckMessage>(legacy.Message).Reason);
        var node = JsonNode.Parse(json)!; node["reason"] = "장애 원인이 남아 있습니다";
        var withReason = new MessageValidator().Validate("amr/A/ack", Bytes(node.ToJsonString()));
        Assert.Equal("장애 원인이 남아 있습니다", Assert.IsType<AckMessage>(withReason.Message).Reason);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("0")]
    [InlineData("true")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("\"\"")]
    [InlineData("\" \"")]
    [InlineData("\"\\uD800\"")]
    public void InvalidAckReasonsAreRejected(string reason)
    {
        var result = new MessageValidator().Validate("amr/A/ack",
            Bytes("{\"cmdId\":\"C1\",\"ts\":1,\"result\":\"FAILURE\",\"reason\":" + reason + "}"));
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"cmdId\":\"C\",\"type\":\"FLY\",\"ts\":1,\"param\":{}}")]
    public async Task PublisherRejectsBeforeAttemptingNetworkIo(string payload)
    {
        using var disconnected = new MqttClientFactory().CreateMqttClient();
        var error = await Assert.ThrowsAsync<ArgumentException>(() => new MqttPublisher(disconnected).PublishCommandAsync("AMR_001", payload));
        Assert.Contains("Invalid Command", error.Message);
    }

    [Theory]
    [InlineData("+")]
    [InlineData("#")]
    [InlineData("A/B")]
    [InlineData("A B")]
    [InlineData("")]
    public void TopicCannotTargetWildcardOrAnotherPath(string id)
        => Assert.Throws<ArgumentException>(() => MqttTopics.Target.Command(id));

    [Fact]
    public void CommandUsesPdfTopic() => Assert.Equal("amr/AMR_001/command", MqttTopics.Target.Command("AMR_001"));
}
