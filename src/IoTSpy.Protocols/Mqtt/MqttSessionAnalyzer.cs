using System.Collections.Concurrent;

namespace IoTSpy.Protocols.Mqtt;

/// <summary>
/// Stateful analyzer that accumulates per-topic statistics and tracks QoS-2 handshake flows
/// across a stream of decoded MQTT messages. Thread-safe; intended as a singleton service.
/// </summary>
public sealed class MqttSessionAnalyzer
{
    private sealed class TopicState
    {
        public long MessageCount;
        public long TotalBytes;
        public long RetainedCount;
        public DateTimeOffset LastSeen;
        public readonly ConcurrentDictionary<MqttQualityOfService, long> QosCounts = new();
    }

    private readonly ConcurrentDictionary<string, TopicState> _topics = new();
    private readonly ConcurrentDictionary<ushort, (string? Topic, MqttQosFlowPhase Phase, DateTimeOffset LastUpdated)> _qosFlows = new();

    /// <summary>
    /// Records a decoded MQTT message, updating topic statistics and QoS-2 flow state.
    /// </summary>
    public void Record(MqttMessage message)
    {
        switch (message.PacketType)
        {
            case MqttPacketType.Publish when message.Topic is not null:
                RecordPublish(message);
                if (message.QoS == MqttQualityOfService.ExactlyOnce && message.PacketId != 0)
                    _qosFlows[message.PacketId] = (message.Topic, MqttQosFlowPhase.Published, DateTimeOffset.UtcNow);
                break;

            case MqttPacketType.PubRec when message.PacketId != 0:
                if (_qosFlows.TryGetValue(message.PacketId, out var recEntry))
                    _qosFlows[message.PacketId] = recEntry with { Phase = MqttQosFlowPhase.Received, LastUpdated = DateTimeOffset.UtcNow };
                break;

            case MqttPacketType.PubRel when message.PacketId != 0:
                if (_qosFlows.TryGetValue(message.PacketId, out var relEntry))
                    _qosFlows[message.PacketId] = relEntry with { Phase = MqttQosFlowPhase.Released, LastUpdated = DateTimeOffset.UtcNow };
                break;

            case MqttPacketType.PubComp when message.PacketId != 0:
                if (_qosFlows.TryGetValue(message.PacketId, out var compEntry))
                    _qosFlows[message.PacketId] = compEntry with { Phase = MqttQosFlowPhase.Completed, LastUpdated = DateTimeOffset.UtcNow };
                break;
        }
    }

    private void RecordPublish(MqttMessage message)
    {
        var topic = message.Topic!;
        var payloadBytes = (long)(message.Payload?.Length ?? 0);
        var state = _topics.GetOrAdd(topic, _ => new TopicState { LastSeen = DateTimeOffset.UtcNow });

        Interlocked.Increment(ref state.MessageCount);
        Interlocked.Add(ref state.TotalBytes, payloadBytes);
        if (message.Retain)
            Interlocked.Increment(ref state.RetainedCount);
        state.QosCounts.AddOrUpdate(message.QoS, 1, (_, v) => v + 1);

        // LastSeen: best-effort (no lock; slight inaccuracy acceptable)
        state.LastSeen = DateTimeOffset.UtcNow;
    }

    /// <summary>Returns a snapshot of per-topic statistics sorted by message count descending.</summary>
    public IReadOnlyList<MqttTopicStatistics> GetTopicStatistics()
    {
        return _topics
            .Select(kvp => new MqttTopicStatistics
            {
                Topic = kvp.Key,
                MessageCount = Interlocked.Read(ref kvp.Value.MessageCount),
                TotalBytes = Interlocked.Read(ref kvp.Value.TotalBytes),
                RetainedCount = Interlocked.Read(ref kvp.Value.RetainedCount),
                LastSeen = kvp.Value.LastSeen,
                QosDistribution = new Dictionary<MqttQualityOfService, long>(kvp.Value.QosCounts)
            })
            .OrderByDescending(s => s.MessageCount)
            .ToList();
    }

    /// <summary>Returns all tracked QoS-2 flows (including completed ones).</summary>
    public IReadOnlyList<MqttQosFlowEntry> GetQosFlows()
    {
        return _qosFlows
            .Select(kvp => new MqttQosFlowEntry(kvp.Key, kvp.Value.Topic, kvp.Value.Phase, kvp.Value.LastUpdated))
            .OrderByDescending(f => f.LastUpdated)
            .ToList();
    }

    /// <summary>Resets all accumulated state.</summary>
    public void Reset()
    {
        _topics.Clear();
        _qosFlows.Clear();
    }
}
