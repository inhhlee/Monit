using System.Text.Json;
using Amr.Communication.Models;
using Amr.Communication.Mqtt;

namespace Amr.Communication.Tests;

// Existing scaffold checks only. These do not certify COM feature verification.
public class ScaffoldTests
{
    [Fact]
    public void TelemetryJsonPreservesTimestampBeyond2038()
    {
        const string json = """{"ts":2208988800,"Pos":{"x":1.25,"y":-2.5,"sp":0.75,"stat":"IDLE"}}""";
        var payload = JsonSerializer.Deserialize<TelemetryPayload>(json);
        Assert.NotNull(payload);
        Assert.Equal(2208988800L, payload.Timestamp);
        Assert.Equal(new PositionData(1.25, -2.5, 0.75, "IDLE"), payload.Position);
        var roundTrip = JsonSerializer.Deserialize<TelemetryPayload>(JsonSerializer.Serialize(payload));
        Assert.Equal(payload, roundTrip);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CommandTopicRejectsMissingAmrId(string? amrId)
    {
        Assert.Throws<ArgumentException>(() => MqttTopics.Target.Command(amrId!));
    }
}
