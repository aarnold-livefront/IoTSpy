using IoTSpy.Proxy.Interception;
using Xunit;

namespace IoTSpy.Proxy.Tests;

public class MqttTopicMatchTests
{
    [Theory]
    [InlineData("sensor/temp", "sensor/temp", true)]
    [InlineData("sensor/temp", "sensor/humidity", false)]
    [InlineData("sensor/+", "sensor/temp", true)]
    [InlineData("sensor/+", "sensor/humidity", true)]
    [InlineData("sensor/+", "sensor/temp/value", false)]
    [InlineData("#", "sensor/temp", true)]
    [InlineData("#", "a/b/c/d", true)]
    [InlineData("sensor/#", "sensor/temp", true)]
    [InlineData("sensor/#", "sensor/temp/value", true)]
    [InlineData("sensor/#", "device/temp", false)]
    [InlineData("+/temp", "sensor/temp", true)]
    [InlineData("+/temp", "device/temp", true)]
    [InlineData("+/temp", "sensor/humidity", false)]
    [InlineData("a/b/c", "a/b", false)]
    [InlineData("a/b", "a/b/c", false)]
    public void MatchesTopic_VariousPatterns(string filter, string topic, bool expected)
    {
        Assert.Equal(expected, MqttBrokerProxy.MatchesTopic(filter, topic));
    }
}
