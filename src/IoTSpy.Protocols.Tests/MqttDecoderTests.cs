using IoTSpy.Protocols.Mqtt;

namespace IoTSpy.Protocols.Tests;

public class MqttDecoderTests
{
    private readonly MqttDecoder _decoder = new();

    // ── CanDecode ────────────────────────────────────────────────────────────

    [Fact]
    public void CanDecode_TooShort_ReturnsFalse()
    {
        Assert.False(_decoder.CanDecode([0x10]));
    }

    [Fact]
    public void CanDecode_ValidConnectHeader_ReturnsTrue()
    {
        // CONNECT packet: upper nibble = 1 (type), lower = 0 (flags)
        Assert.True(_decoder.CanDecode([0x10, 0x00]));
    }

    [Fact]
    public void CanDecode_ValidPublishHeader_ReturnsTrue()
    {
        // PUBLISH packet: upper nibble = 3
        Assert.True(_decoder.CanDecode([0x30, 0x00]));
    }

    [Fact]
    public void CanDecode_InvalidPacketType_Zero_ReturnsFalse()
    {
        // Upper nibble = 0 is invalid per MQTT spec (types are 1-15)
        Assert.False(_decoder.CanDecode([0x00, 0x00]));
    }

    // ── DecodeAsync: PINGREQ ─────────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_PingReq_ReturnsOneMessage()
    {
        // PINGREQ: fixed header 0xC0 (type=12, flags=0), remaining=0
        byte[] data = [0xC0, 0x00];

        var messages = await _decoder.DecodeAsync(data);

        Assert.Single(messages);
        Assert.Equal(MqttPacketType.PingReq, messages[0].PacketType);
        Assert.Equal(2, messages[0].TotalLength);
    }

    [Fact]
    public async Task DecodeAsync_PingResp_ReturnsCorrectType()
    {
        // PINGRESP: 0xD0 (type=13)
        byte[] data = [0xD0, 0x00];

        var messages = await _decoder.DecodeAsync(data);

        Assert.Single(messages);
        Assert.Equal(MqttPacketType.PingResp, messages[0].PacketType);
    }

    // ── DecodeAsync: CONNECT ────────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_ConnectPacket_DecodesClientId()
    {
        // CONNECT (3.1.1) with clientId="testclid", cleanSession=true, keepAlive=60
        // Fixed header: 0x10, remaining=20
        // Variable header: "MQTT" (6 bytes) + level(1) + flags(1) + keepalive(2) = 10
        // Payload: clientId length(2) + "testclid"(8) = 10
        byte[] data =
        [
            0x10, 0x14,                         // CONNECT, remaining=20
            0x00, 0x04, 0x4D, 0x51, 0x54, 0x54, // "MQTT"
            0x04,                               // Protocol level 4 (3.1.1)
            0x02,                               // Connect flags: cleanSession=1
            0x00, 0x3C,                         // KeepAlive=60
            0x00, 0x08,                         // ClientId length=8
            0x74, 0x65, 0x73, 0x74, 0x63, 0x6C, 0x69, 0x64 // "testclid"
        ];

        var messages = await _decoder.DecodeAsync(data);

        Assert.Single(messages);
        var msg = messages[0];
        Assert.Equal(MqttPacketType.Connect, msg.PacketType);
        Assert.Equal(MqttVersion.V311, msg.Version);
        Assert.Equal("testclid", msg.ClientId);
        Assert.True(msg.CleanSession);
        Assert.Equal(60, msg.KeepAliveSeconds);
        Assert.False(msg.HasPassword);
    }

    [Fact]
    public async Task DecodeAsync_ConnectWithUsername_DecodesUsername()
    {
        // CONNECT with username="admin", cleanSession=true
        // Connect flags: 0x82 = 1000 0010 (username=1, cleanSession=1)
        // Variable header: "MQTT"(6) + level(1) + flags(1) + keepalive(2) = 10
        // Payload: clientId(2+4="test") + username(2+5="admin") = 6+7 = 13
        // Total remaining = 10 + 13 = 23 = 0x17
        byte[] data =
        [
            0x10, 0x17,
            0x00, 0x04, 0x4D, 0x51, 0x54, 0x54,
            0x04,
            0x82, // flags: cleanSession + username
            0x00, 0x3C,
            0x00, 0x04, (byte)'t', (byte)'e', (byte)'s', (byte)'t', // clientId="test"
            0x00, 0x05, (byte)'a', (byte)'d', (byte)'m', (byte)'i', (byte)'n' // username="admin"
        ];

        var messages = await _decoder.DecodeAsync(data);

        Assert.Single(messages);
        Assert.Equal("admin", messages[0].Username);
        Assert.False(messages[0].HasPassword);
    }

    // ── DecodeAsync: PUBLISH ────────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_PublishQos0_DecodesTopicAndPayload()
    {
        // PUBLISH QoS=0: fixed header 0x30, remaining = 2+4 + 5 = 11
        // Topic: "test" (4 chars)
        // Payload: "hello" (5 chars)
        byte[] data =
        [
            0x30, 0x0B,
            0x00, 0x04, (byte)'t', (byte)'e', (byte)'s', (byte)'t',
            (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o'
        ];

        var messages = await _decoder.DecodeAsync(data);

        Assert.Single(messages);
        var msg = messages[0];
        Assert.Equal(MqttPacketType.Publish, msg.PacketType);
        Assert.Equal("test", msg.Topic);
        Assert.Equal("hello", msg.PayloadString);
        Assert.Equal(MqttQualityOfService.AtMostOnce, msg.QoS);
        Assert.False(msg.Retain);
        Assert.False(msg.Duplicate);
    }

    [Fact]
    public async Task DecodeAsync_PublishQos1_IncludesPacketId()
    {
        // PUBLISH QoS=1: fixed header 0x32 (type=3, flags=0010)
        // remaining = 2+4 + 2(packetId) + 5 = 13
        byte[] data =
        [
            0x32, 0x0D,
            0x00, 0x04, (byte)'t', (byte)'e', (byte)'s', (byte)'t',
            0x00, 0x05, // PacketId = 5
            (byte)'h', (byte)'e', (byte)'l', (byte)'l', (byte)'o'
        ];

        var messages = await _decoder.DecodeAsync(data);

        Assert.Single(messages);
        var msg = messages[0];
        Assert.Equal(MqttPacketType.Publish, msg.PacketType);
        Assert.Equal(MqttQualityOfService.AtLeastOnce, msg.QoS);
        Assert.Equal(5, msg.PacketId);
        Assert.Equal("test", msg.Topic);
    }

    [Fact]
    public async Task DecodeAsync_PublishRetain_SetsRetainFlag()
    {
        // PUBLISH with retain=1: fixed header 0x31 (flags=0001)
        byte[] data =
        [
            0x31, 0x07,
            0x00, 0x04, (byte)'t', (byte)'e', (byte)'s', (byte)'t',
            (byte)'!'
        ];

        var messages = await _decoder.DecodeAsync(data);

        Assert.Single(messages);
        Assert.True(messages[0].Retain);
    }

    // ── DecodeAsync: CONNACK ─────────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_ConnAckAccepted_DecodesReturnCode()
    {
        // CONNACK: 0x20, remaining=2, ackFlags=0x00, returnCode=0x00 (accepted)
        byte[] data = [0x20, 0x02, 0x00, 0x00];

        var messages = await _decoder.DecodeAsync(data);

        Assert.Single(messages);
        var msg = messages[0];
        Assert.Equal(MqttPacketType.ConnAck, msg.PacketType);
        Assert.Equal(0, msg.ConnectReturnCode);
        Assert.False(msg.SessionPresent);
    }

    [Fact]
    public async Task DecodeAsync_ConnAckSessionPresent_SetsSPFlag()
    {
        // CONNACK with sessionPresent=1: ackFlags=0x01
        byte[] data = [0x20, 0x02, 0x01, 0x00];

        var messages = await _decoder.DecodeAsync(data);

        Assert.Single(messages);
        Assert.True(messages[0].SessionPresent);
    }

    // ── DecodeAsync: SUBSCRIBE ───────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_Subscribe_DecodesTopicFilters()
    {
        // SUBSCRIBE: 0x82, remaining = 2(packetId) + 2+4(topic len+name) + 1(qos) = 9
        byte[] data =
        [
            0x82, 0x09,
            0x00, 0x01, // PacketId=1
            0x00, 0x04, (byte)'t', (byte)'e', (byte)'s', (byte)'t', // topic="test"
            0x01  // QoS=1
        ];

        var messages = await _decoder.DecodeAsync(data);

        Assert.Single(messages);
        var msg = messages[0];
        Assert.Equal(MqttPacketType.Subscribe, msg.PacketType);
        Assert.Equal(1, msg.PacketId);
        Assert.Single(msg.Subscriptions!);
        Assert.Equal("test", msg.Subscriptions![0].TopicFilter);
        Assert.Equal(MqttQualityOfService.AtLeastOnce, msg.Subscriptions![0].QoS);
    }

    // ── DecodeAsync: DISCONNECT ──────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_Disconnect_ReturnsDisconnectPacket()
    {
        // DISCONNECT: 0xE0, remaining=0
        byte[] data = [0xE0, 0x00];

        var messages = await _decoder.DecodeAsync(data);

        Assert.Single(messages);
        Assert.Equal(MqttPacketType.Disconnect, messages[0].PacketType);
    }

    // ── DecodeAsync: multiple packets in one buffer ──────────────────────────

    [Fact]
    public async Task DecodeAsync_TwoPackets_ReturnsBoth()
    {
        // PINGREQ followed by PINGRESP
        byte[] data = [0xC0, 0x00, 0xD0, 0x00];

        var messages = await _decoder.DecodeAsync(data);

        Assert.Equal(2, messages.Count);
        Assert.Equal(MqttPacketType.PingReq, messages[0].PacketType);
        Assert.Equal(MqttPacketType.PingResp, messages[1].PacketType);
    }

    // ── DecodeAsync: insufficient data ──────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_TruncatedPacket_ReturnsEmpty()
    {
        // CONNECT says remaining=20 but only 2 bytes of payload
        byte[] data = [0x10, 0x14, 0x00, 0x04];

        var messages = await _decoder.DecodeAsync(data);

        Assert.Empty(messages);
    }

    [Fact]
    public async Task DecodeAsync_EmptyBuffer_ReturnsEmpty()
    {
        var messages = await _decoder.DecodeAsync(Array.Empty<byte>());

        Assert.Empty(messages);
    }
}
