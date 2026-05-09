using IoTSpy.Protocols.Mqtt;
using Xunit;

namespace IoTSpy.Protocols.Tests;

public class MqttSessionAnalyzerTests
{
    private static MqttMessage Publish(string topic, int payloadBytes = 10, bool retain = false,
        MqttQualityOfService qos = MqttQualityOfService.AtMostOnce, ushort packetId = 0) =>
        new()
        {
            PacketType = MqttPacketType.Publish,
            Topic = topic,
            Payload = new byte[payloadBytes],
            Retain = retain,
            QoS = qos,
            PacketId = packetId
        };

    // ── GetTopicStatistics ────────────────────────────────────────────────────

    [Fact]
    public void Record_SinglePublish_AccumulatesTopicStats()
    {
        var analyzer = new MqttSessionAnalyzer();
        analyzer.Record(Publish("sensors/temp", payloadBytes: 20));

        var stats = analyzer.GetTopicStatistics();

        Assert.Single(stats);
        Assert.Equal("sensors/temp", stats[0].Topic);
        Assert.Equal(1, stats[0].MessageCount);
        Assert.Equal(20, stats[0].TotalBytes);
        Assert.Equal(0, stats[0].RetainedCount);
    }

    [Fact]
    public void Record_MultiplePublishes_SumsByteCount()
    {
        var analyzer = new MqttSessionAnalyzer();
        analyzer.Record(Publish("t", payloadBytes: 5));
        analyzer.Record(Publish("t", payloadBytes: 15));

        var stats = analyzer.GetTopicStatistics();

        Assert.Equal(2, stats[0].MessageCount);
        Assert.Equal(20, stats[0].TotalBytes);
    }

    [Fact]
    public void Record_RetainedMessage_IncrementsRetainedCount()
    {
        var analyzer = new MqttSessionAnalyzer();
        analyzer.Record(Publish("t", retain: true));
        analyzer.Record(Publish("t", retain: false));

        var stats = analyzer.GetTopicStatistics();

        Assert.Equal(2, stats[0].MessageCount);
        Assert.Equal(1, stats[0].RetainedCount);
    }

    [Fact]
    public void Record_QosDistribution_TracksPerQosLevel()
    {
        var analyzer = new MqttSessionAnalyzer();
        analyzer.Record(Publish("t", qos: MqttQualityOfService.AtMostOnce));
        analyzer.Record(Publish("t", qos: MqttQualityOfService.AtLeastOnce, packetId: 1));
        analyzer.Record(Publish("t", qos: MqttQualityOfService.AtLeastOnce, packetId: 2));

        var stats = analyzer.GetTopicStatistics();
        var qos = stats[0].QosDistribution;

        Assert.Equal(1, qos[MqttQualityOfService.AtMostOnce]);
        Assert.Equal(2, qos[MqttQualityOfService.AtLeastOnce]);
    }

    [Fact]
    public void GetTopicStatistics_SortedByMessageCountDescending()
    {
        var analyzer = new MqttSessionAnalyzer();
        analyzer.Record(Publish("low"));
        for (var i = 0; i < 5; i++) analyzer.Record(Publish("high"));

        var stats = analyzer.GetTopicStatistics();

        Assert.Equal("high", stats[0].Topic);
        Assert.Equal("low", stats[1].Topic);
    }

    [Fact]
    public void Record_NonPublishPacket_DoesNotAddTopic()
    {
        var analyzer = new MqttSessionAnalyzer();
        analyzer.Record(new MqttMessage { PacketType = MqttPacketType.PingReq });

        Assert.Empty(analyzer.GetTopicStatistics());
    }

    // ── QoS-2 flow tracking ───────────────────────────────────────────────────

    [Fact]
    public void Record_QoS2Flow_TracksPhases()
    {
        var analyzer = new MqttSessionAnalyzer();
        ushort pid = 42;

        analyzer.Record(Publish("t", qos: MqttQualityOfService.ExactlyOnce, packetId: pid));
        var flows = analyzer.GetQosFlows();
        Assert.Single(flows);
        Assert.Equal(MqttQosFlowPhase.Published, flows[0].Phase);
        Assert.Equal("t", flows[0].Topic);

        analyzer.Record(new MqttMessage { PacketType = MqttPacketType.PubRec, PacketId = pid });
        Assert.Equal(MqttQosFlowPhase.Received, analyzer.GetQosFlows()[0].Phase);

        analyzer.Record(new MqttMessage { PacketType = MqttPacketType.PubRel, PacketId = pid });
        Assert.Equal(MqttQosFlowPhase.Released, analyzer.GetQosFlows()[0].Phase);

        analyzer.Record(new MqttMessage { PacketType = MqttPacketType.PubComp, PacketId = pid });
        Assert.Equal(MqttQosFlowPhase.Completed, analyzer.GetQosFlows()[0].Phase);
    }

    [Fact]
    public void Record_PubRecWithoutPublish_IsNoOp()
    {
        var analyzer = new MqttSessionAnalyzer();

        // PUBREC for a packet ID we never saw PUBLISH for
        analyzer.Record(new MqttMessage { PacketType = MqttPacketType.PubRec, PacketId = 99 });

        Assert.Empty(analyzer.GetQosFlows());
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsAllState()
    {
        var analyzer = new MqttSessionAnalyzer();
        analyzer.Record(Publish("t"));
        analyzer.Record(Publish("t", qos: MqttQualityOfService.ExactlyOnce, packetId: 1));

        analyzer.Reset();

        Assert.Empty(analyzer.GetTopicStatistics());
        Assert.Empty(analyzer.GetQosFlows());
    }
}
