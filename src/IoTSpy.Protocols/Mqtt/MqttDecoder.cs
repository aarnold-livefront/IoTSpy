using System.Buffers.Binary;
using System.Text;
using IoTSpy.Core.Interfaces;

namespace IoTSpy.Protocols.Mqtt;

/// <summary>
/// Decodes raw bytes into MQTT control packets. Supports MQTT 3.1.1 and 5.0.
/// The decoder is stateless; each call to <see cref="DecodeAsync"/> is independent.
/// </summary>
public sealed class MqttDecoder : IProtocolDecoder<MqttMessage>
{
    /// <summary>
    /// MQTT always starts with a fixed header whose upper nibble is the packet type (1-15)
    /// and byte 2+ is the variable-length remaining length. CONNECT packets start with
    /// 0x10 (type=1, flags=0). We sniff the first byte: upper nibble must be 1-15.
    /// </summary>
    public bool CanDecode(ReadOnlySpan<byte> header)
    {
        if (header.Length < 2) return false;
        var packetType = (byte)(header[0] >> 4);
        return packetType is >= 1 and <= 15;
    }

    public Task<IReadOnlyList<MqttMessage>> DecodeAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var messages = new List<MqttMessage>();
        var span = data.Span;
        var offset = 0;

        while (offset < span.Length && !ct.IsCancellationRequested)
        {
            if (!TryDecodePacket(span[offset..], out var msg, out var consumed))
                break;

            messages.Add(msg);
            offset += consumed;
        }

        return Task.FromResult<IReadOnlyList<MqttMessage>>(messages);
    }

    private static bool TryDecodePacket(ReadOnlySpan<byte> span, out MqttMessage message, out int consumed)
    {
        message = default!;
        consumed = 0;

        if (span.Length < 2) return false;

        // Fixed header byte 1
        var byte1 = span[0];
        var packetType = (MqttPacketType)(byte1 >> 4);
        var flags = (byte)(byte1 & 0x0F);

        // Remaining length (variable-length encoding, max 4 bytes)
        if (!TryDecodeRemainingLength(span[1..], out var remainingLength, out var lenBytes))
            return false;

        var headerSize = 1 + lenBytes;
        var totalLength = headerSize + remainingLength;
        if (span.Length < totalLength) return false;

        var payload = span[headerSize..totalLength];
        var rawBytes = span[..totalLength].ToArray();

        message = packetType switch
        {
            MqttPacketType.Connect => DecodeConnect(payload, flags, rawBytes),
            MqttPacketType.ConnAck => DecodeConnAck(payload, flags, rawBytes),
            MqttPacketType.Publish => DecodePublish(payload, flags, rawBytes),
            MqttPacketType.PubAck or MqttPacketType.PubRec or
            MqttPacketType.PubRel or MqttPacketType.PubComp => DecodeAckPacket(packetType, payload, flags, rawBytes),
            MqttPacketType.Subscribe => DecodeSubscribe(payload, flags, rawBytes),
            MqttPacketType.SubAck => DecodeSubAck(payload, flags, rawBytes),
            MqttPacketType.Unsubscribe => DecodeUnsubscribe(payload, flags, rawBytes),
            MqttPacketType.UnsubAck => DecodeUnsubAck(packetType, payload, flags, rawBytes),
            MqttPacketType.PingReq or MqttPacketType.PingResp => new MqttMessage
            {
                PacketType = packetType,
                TotalLength = totalLength,
                RawBytes = rawBytes
            },
            MqttPacketType.Disconnect => DecodeDisconnect(payload, flags, rawBytes),
            MqttPacketType.Auth => DecodeAuth(payload, flags, rawBytes),
            _ => new MqttMessage
            {
                PacketType = packetType,
                TotalLength = totalLength,
                RawBytes = rawBytes
            }
        };

        consumed = totalLength;
        return true;
    }

    // ── CONNECT ──────────────────────────────────────────────────────────────

    private static MqttMessage DecodeConnect(ReadOnlySpan<byte> payload, byte flags, byte[] rawBytes)
    {
        var pos = 0;

        // Variable header: Protocol Name (length-prefixed UTF-8 string)
        var protocolName = ReadUtf8String(payload, ref pos);

        // Protocol Level (1 byte)
        var protocolLevel = payload[pos++];
        var version = protocolLevel switch
        {
            4 => MqttVersion.V311,
            5 => MqttVersion.V500,
            _ => MqttVersion.Unknown
        };

        // Connect Flags (1 byte)
        var connectFlags = payload[pos++];
        var hasUsername = (connectFlags & 0x80) != 0;
        var hasPassword = (connectFlags & 0x40) != 0;
        var willRetain = (connectFlags & 0x20) != 0;
        var willQoS = (MqttQualityOfService)((connectFlags >> 3) & 0x03);
        var hasWill = (connectFlags & 0x04) != 0;
        var cleanSession = (connectFlags & 0x02) != 0;

        // Keep Alive (2 bytes, big-endian)
        var keepAlive = BinaryPrimitives.ReadUInt16BigEndian(payload[pos..]);
        pos += 2;

        // MQTT 5.0: Properties
        IReadOnlyDictionary<byte, byte[]>? properties = null;
        if (version == MqttVersion.V500)
            properties = ReadProperties(payload, ref pos);

        // Payload: ClientId
        var clientId = ReadUtf8String(payload, ref pos);

        // Will
        string? willTopic = null;
        byte[]? willPayload = null;
        if (hasWill)
        {
            if (version == MqttVersion.V500)
                ReadProperties(payload, ref pos); // Will Properties (skip)
            willTopic = ReadUtf8String(payload, ref pos);
            willPayload = ReadBinaryData(payload, ref pos);
        }

        // Username
        string? username = null;
        if (hasUsername && pos < payload.Length)
            username = ReadUtf8String(payload, ref pos);

        // Password (skip reading actual value for security)
        if (hasPassword && pos < payload.Length)
            ReadBinaryData(payload, ref pos); // consume but don't store

        return new MqttMessage
        {
            PacketType = MqttPacketType.Connect,
            Version = version,
            TotalLength = rawBytes.Length,
            ClientId = clientId,
            Username = username,
            HasPassword = hasPassword,
            CleanSession = cleanSession,
            KeepAliveSeconds = keepAlive,
            WillTopic = willTopic,
            WillPayload = willPayload,
            WillQoS = willQoS,
            WillRetain = willRetain,
            Properties = properties,
            RawBytes = rawBytes
        };
    }

    // ── CONNACK ──────────────────────────────────────────────────────────────

    private static MqttMessage DecodeConnAck(ReadOnlySpan<byte> payload, byte flags, byte[] rawBytes)
    {
        var pos = 0;
        var ackFlags = payload[pos++];
        var sessionPresent = (ackFlags & 0x01) != 0;
        var returnCode = payload[pos++];

        IReadOnlyDictionary<byte, byte[]>? properties = null;
        if (pos < payload.Length)
            properties = ReadProperties(payload, ref pos);

        return new MqttMessage
        {
            PacketType = MqttPacketType.ConnAck,
            TotalLength = rawBytes.Length,
            SessionPresent = sessionPresent,
            ConnectReturnCode = returnCode,
            Properties = properties,
            RawBytes = rawBytes
        };
    }

    // ── PUBLISH ──────────────────────────────────────────────────────────────

    private static MqttMessage DecodePublish(ReadOnlySpan<byte> payload, byte flags, byte[] rawBytes)
    {
        var dup = (flags & 0x08) != 0;
        var qos = (MqttQualityOfService)((flags >> 1) & 0x03);
        var retain = (flags & 0x01) != 0;

        var pos = 0;
        var topic = ReadUtf8String(payload, ref pos);

        ushort packetId = 0;
        if (qos > MqttQualityOfService.AtMostOnce)
        {
            packetId = BinaryPrimitives.ReadUInt16BigEndian(payload[pos..]);
            pos += 2;
        }

        // Remaining bytes are the application payload
        var msgPayload = payload[pos..].ToArray();

        return new MqttMessage
        {
            PacketType = MqttPacketType.Publish,
            TotalLength = rawBytes.Length,
            Duplicate = dup,
            QoS = qos,
            Retain = retain,
            Topic = topic,
            PacketId = packetId,
            Payload = msgPayload,
            RawBytes = rawBytes
        };
    }

    // ── PUBACK / PUBREC / PUBREL / PUBCOMP ───────────────────────────────────

    private static MqttMessage DecodeAckPacket(MqttPacketType type, ReadOnlySpan<byte> payload, byte flags, byte[] rawBytes)
    {
        var pos = 0;
        ushort packetId = 0;
        byte reasonCode = 0;

        if (payload.Length >= 2)
        {
            packetId = BinaryPrimitives.ReadUInt16BigEndian(payload[pos..]);
            pos += 2;
        }
        if (payload.Length >= 3)
            reasonCode = payload[pos++];

        IReadOnlyDictionary<byte, byte[]>? properties = null;
        if (pos < payload.Length)
            properties = ReadProperties(payload, ref pos);

        return new MqttMessage
        {
            PacketType = type,
            TotalLength = rawBytes.Length,
            PacketId = packetId,
            ReasonCode = reasonCode,
            Properties = properties,
            RawBytes = rawBytes
        };
    }

    // ── SUBSCRIBE ────────────────────────────────────────────────────────────

    private static MqttMessage DecodeSubscribe(ReadOnlySpan<byte> payload, byte flags, byte[] rawBytes)
    {
        var pos = 0;
        var packetId = BinaryPrimitives.ReadUInt16BigEndian(payload[pos..]);
        pos += 2;

        // MQTT 5.0 properties (check if there's a property length)
        IReadOnlyDictionary<byte, byte[]>? properties = null;
        // For 5.0, next byte is properties length — we can't know version here,
        // so we attempt a heuristic: if the next bytes form a valid topic filter
        // length that makes sense, assume 3.1.1; otherwise try 5.0 properties.
        // In practice, callers should track version from CONNECT.
        // For robustness, we just parse topic filters from the current position.

        var subs = new List<MqttSubscription>();
        while (pos < payload.Length)
        {
            var topicFilter = ReadUtf8String(payload, ref pos);
            var qos = (MqttQualityOfService)(payload[pos++] & 0x03);
            subs.Add(new MqttSubscription(topicFilter, qos));
        }

        return new MqttMessage
        {
            PacketType = MqttPacketType.Subscribe,
            TotalLength = rawBytes.Length,
            PacketId = packetId,
            Subscriptions = subs,
            Properties = properties,
            RawBytes = rawBytes
        };
    }

    // ── SUBACK ───────────────────────────────────────────────────────────────

    private static MqttMessage DecodeSubAck(ReadOnlySpan<byte> payload, byte flags, byte[] rawBytes)
    {
        var pos = 0;
        var packetId = BinaryPrimitives.ReadUInt16BigEndian(payload[pos..]);
        pos += 2;

        var grantedQoS = new List<byte>();
        while (pos < payload.Length)
            grantedQoS.Add(payload[pos++]);

        return new MqttMessage
        {
            PacketType = MqttPacketType.SubAck,
            TotalLength = rawBytes.Length,
            PacketId = packetId,
            GrantedQoSLevels = grantedQoS,
            RawBytes = rawBytes
        };
    }

    // ── UNSUBSCRIBE ──────────────────────────────────────────────────────────

    private static MqttMessage DecodeUnsubscribe(ReadOnlySpan<byte> payload, byte flags, byte[] rawBytes)
    {
        var pos = 0;
        var packetId = BinaryPrimitives.ReadUInt16BigEndian(payload[pos..]);
        pos += 2;

        var subs = new List<MqttSubscription>();
        while (pos < payload.Length)
        {
            var topicFilter = ReadUtf8String(payload, ref pos);
            subs.Add(new MqttSubscription(topicFilter, MqttQualityOfService.AtMostOnce));
        }

        return new MqttMessage
        {
            PacketType = MqttPacketType.Unsubscribe,
            TotalLength = rawBytes.Length,
            PacketId = packetId,
            Subscriptions = subs,
            RawBytes = rawBytes
        };
    }

    // ── UNSUBACK ─────────────────────────────────────────────────────────────

    private static MqttMessage DecodeUnsubAck(MqttPacketType type, ReadOnlySpan<byte> payload, byte flags, byte[] rawBytes)
    {
        var pos = 0;
        ushort packetId = 0;
        if (payload.Length >= 2)
        {
            packetId = BinaryPrimitives.ReadUInt16BigEndian(payload[pos..]);
            pos += 2;
        }

        return new MqttMessage
        {
            PacketType = type,
            TotalLength = rawBytes.Length,
            PacketId = packetId,
            RawBytes = rawBytes
        };
    }

    // ── DISCONNECT (5.0 has reason code + properties) ────────────────────────

    private static MqttMessage DecodeDisconnect(ReadOnlySpan<byte> payload, byte flags, byte[] rawBytes)
    {
        byte reasonCode = 0;
        IReadOnlyDictionary<byte, byte[]>? properties = null;
        var pos = 0;

        if (payload.Length >= 1)
            reasonCode = payload[pos++];
        if (pos < payload.Length)
            properties = ReadProperties(payload, ref pos);

        return new MqttMessage
        {
            PacketType = MqttPacketType.Disconnect,
            TotalLength = rawBytes.Length,
            ReasonCode = reasonCode,
            Properties = properties,
            RawBytes = rawBytes
        };
    }

    // ── AUTH (5.0 only) ──────────────────────────────────────────────────────

    private static MqttMessage DecodeAuth(ReadOnlySpan<byte> payload, byte flags, byte[] rawBytes)
    {
        byte reasonCode = 0;
        IReadOnlyDictionary<byte, byte[]>? properties = null;
        var pos = 0;

        if (payload.Length >= 1)
            reasonCode = payload[pos++];
        if (pos < payload.Length)
            properties = ReadProperties(payload, ref pos);

        return new MqttMessage
        {
            PacketType = MqttPacketType.Auth,
            TotalLength = rawBytes.Length,
            ReasonCode = reasonCode,
            Properties = properties,
            RawBytes = rawBytes
        };
    }

    // ── Wire-format helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Decodes the MQTT variable-length remaining-length field (1-4 bytes).
    /// </summary>
    private static bool TryDecodeRemainingLength(ReadOnlySpan<byte> span, out int value, out int bytesRead)
    {
        value = 0;
        var multiplier = 1;
        bytesRead = 0;

        for (var i = 0; i < 4 && i < span.Length; i++)
        {
            var encoded = span[i];
            value += (encoded & 0x7F) * multiplier;
            multiplier *= 128;
            bytesRead++;
            if ((encoded & 0x80) == 0)
                return true;
        }

        return false; // malformed
    }

    /// <summary>Reads a length-prefixed UTF-8 string (2-byte big-endian length prefix).</summary>
    private static string ReadUtf8String(ReadOnlySpan<byte> span, ref int pos)
    {
        if (pos + 2 > span.Length) return string.Empty;
        var len = BinaryPrimitives.ReadUInt16BigEndian(span[pos..]);
        pos += 2;
        if (pos + len > span.Length) return string.Empty;
        var s = Encoding.UTF8.GetString(span.Slice(pos, len));
        pos += len;
        return s;
    }

    /// <summary>Reads a length-prefixed binary data field (2-byte big-endian length prefix).</summary>
    private static byte[] ReadBinaryData(ReadOnlySpan<byte> span, ref int pos)
    {
        if (pos + 2 > span.Length) return [];
        var len = BinaryPrimitives.ReadUInt16BigEndian(span[pos..]);
        pos += 2;
        if (pos + len > span.Length) return [];
        var data = span.Slice(pos, len).ToArray();
        pos += len;
        return data;
    }

    /// <summary>
    /// Reads MQTT 5.0 property set. Returns a dictionary keyed by property identifier.
    /// </summary>
    private static IReadOnlyDictionary<byte, byte[]>? ReadProperties(ReadOnlySpan<byte> span, ref int pos)
    {
        if (pos >= span.Length) return null;

        // Properties length is a Variable Byte Integer
        if (!TryDecodeVariableInt(span[pos..], out var propsLength, out var propsLenBytes))
            return null;
        pos += propsLenBytes;

        if (propsLength == 0) return null;

        var endPos = pos + propsLength;
        if (endPos > span.Length) endPos = span.Length;

        var props = new Dictionary<byte, byte[]>();
        while (pos < endPos)
        {
            var propId = span[pos++];
            // Read the property value. For simplicity, we store raw bytes.
            // Known property IDs could be decoded further, but raw storage
            // allows callers to interpret as needed.
            var remaining = endPos - pos;
            var value = GetPropertyValue(span, propId, ref pos, endPos);
            props[propId] = value;
        }

        pos = endPos;
        return props;
    }

    private static bool TryDecodeVariableInt(ReadOnlySpan<byte> span, out int value, out int bytesRead)
        => TryDecodeRemainingLength(span, out value, out bytesRead);

    /// <summary>
    /// Reads a single MQTT 5.0 property value based on the property identifier.
    /// </summary>
    private static byte[] GetPropertyValue(ReadOnlySpan<byte> span, byte propId, ref int pos, int endPos)
    {
        // Property types per MQTT 5.0 spec §2.2.2.2
        return propId switch
        {
            // Byte properties
            0x01 or 0x17 or 0x19 or 0x24 or 0x25 or 0x27 or 0x28 or 0x29 or 0x2A =>
                ReadFixedBytes(span, ref pos, 1),

            // Two-byte integer properties
            0x13 or 0x21 or 0x22 or 0x23 or 0x33 or 0x34 or 0x35 =>
                ReadFixedBytes(span, ref pos, 2),

            // Four-byte integer properties
            0x02 or 0x11 or 0x18 or 0x27 =>
                ReadFixedBytes(span, ref pos, 4),

            // Variable byte integer properties
            0x0B =>
                ReadVariableIntBytes(span, ref pos),

            // UTF-8 string properties
            0x03 or 0x08 or 0x09 or 0x12 or 0x15 or 0x16 or 0x1A or 0x1C or 0x1F =>
                ReadLengthPrefixedBytes(span, ref pos),

            // Binary data properties
            0x09 or 0x16 =>
                ReadLengthPrefixedBytes(span, ref pos),

            // UTF-8 string pair (User Property 0x26)
            0x26 =>
                ReadStringPairBytes(span, ref pos),

            // Unknown — consume remaining to avoid infinite loop
            _ => ReadRemainingBytes(span, ref pos, endPos)
        };
    }

    private static byte[] ReadFixedBytes(ReadOnlySpan<byte> span, ref int pos, int count)
    {
        if (pos + count > span.Length) return [];
        var result = span.Slice(pos, count).ToArray();
        pos += count;
        return result;
    }

    private static byte[] ReadVariableIntBytes(ReadOnlySpan<byte> span, ref int pos)
    {
        var start = pos;
        for (var i = 0; i < 4 && pos < span.Length; i++)
        {
            if ((span[pos++] & 0x80) == 0) break;
        }
        return span[start..pos].ToArray();
    }

    private static byte[] ReadLengthPrefixedBytes(ReadOnlySpan<byte> span, ref int pos)
    {
        if (pos + 2 > span.Length) return [];
        var len = BinaryPrimitives.ReadUInt16BigEndian(span[pos..]);
        var totalLen = 2 + len;
        if (pos + totalLen > span.Length) return [];
        var result = span.Slice(pos, totalLen).ToArray();
        pos += totalLen;
        return result;
    }

    private static byte[] ReadStringPairBytes(ReadOnlySpan<byte> span, ref int pos)
    {
        var start = pos;
        // Key
        if (pos + 2 > span.Length) return [];
        var keyLen = BinaryPrimitives.ReadUInt16BigEndian(span[pos..]);
        pos += 2 + keyLen;
        // Value
        if (pos + 2 > span.Length) return span[start..pos].ToArray();
        var valLen = BinaryPrimitives.ReadUInt16BigEndian(span[pos..]);
        pos += 2 + valLen;
        return span[start..pos].ToArray();
    }

    private static byte[] ReadRemainingBytes(ReadOnlySpan<byte> span, ref int pos, int endPos)
    {
        var remaining = Math.Min(endPos - pos, span.Length - pos);
        if (remaining <= 0) return [];
        var result = span.Slice(pos, remaining).ToArray();
        pos += remaining;
        return result;
    }
}
