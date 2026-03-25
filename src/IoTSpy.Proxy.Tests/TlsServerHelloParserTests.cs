using IoTSpy.Proxy.Tls;
using Xunit;

namespace IoTSpy.Proxy.Tests;

public class TlsServerHelloParserTests
{
    /// <summary>
    /// Builds a minimal TLS 1.2 ServerHello record.
    /// </summary>
    private static byte[] BuildServerHello(ushort cipherSuite = 0xC02F, ushort version = 0x0303, ushort[]? extensions = null)
    {
        extensions ??= [];

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // Build handshake body
        using var hsMs = new MemoryStream();
        using var hs = new BinaryWriter(hsMs);

        // ServerVersion
        hs.Write((byte)(version >> 8));
        hs.Write((byte)version);

        // Random (32 bytes)
        hs.Write(new byte[32]);

        // Session ID (0 length)
        hs.Write((byte)0);

        // Cipher suite
        hs.Write((byte)(cipherSuite >> 8));
        hs.Write((byte)cipherSuite);

        // Compression method
        hs.Write((byte)0);

        // Extensions
        if (extensions.Length > 0)
        {
            using var extMs = new MemoryStream();
            using var extBw = new BinaryWriter(extMs);
            foreach (var extType in extensions)
            {
                extBw.Write((byte)(extType >> 8));
                extBw.Write((byte)extType);
                extBw.Write((byte)0); // ext length = 0
                extBw.Write((byte)0);
            }
            var extBytes = extMs.ToArray();
            hs.Write((byte)(extBytes.Length >> 8));
            hs.Write((byte)extBytes.Length);
            hs.Write(extBytes);
        }

        var hsBody = hsMs.ToArray();

        // TLS record header
        bw.Write((byte)0x16); // Handshake
        bw.Write((byte)0x03);
        bw.Write((byte)0x03); // TLS 1.2 record
        var recordLen = 4 + hsBody.Length;
        bw.Write((byte)(recordLen >> 8));
        bw.Write((byte)recordLen);

        // Handshake: type=ServerHello(2), length (3 bytes)
        bw.Write((byte)0x02);
        bw.Write((byte)(hsBody.Length >> 16));
        bw.Write((byte)(hsBody.Length >> 8));
        bw.Write((byte)hsBody.Length);
        bw.Write(hsBody);

        return ms.ToArray();
    }

    [Fact]
    public void TryParseServerHello_ValidRecord_ExtractsCipherSuite()
    {
        var data = BuildServerHello(cipherSuite: 0xC02F);
        var success = TlsServerHelloParser.TryParseServerHello(data, out var info);

        Assert.True(success);
        Assert.Equal(0xC02F, info.CipherSuite);
    }

    [Fact]
    public void TryParseServerHello_ValidRecord_ExtractsTlsVersion()
    {
        var data = BuildServerHello(version: 0x0303);
        var success = TlsServerHelloParser.TryParseServerHello(data, out var info);

        Assert.True(success);
        Assert.Equal(0x0303, info.TlsVersion); // TLS 1.2
    }

    [Fact]
    public void TryParseServerHello_ValidRecord_ComputesJa3sHash()
    {
        var data = BuildServerHello(cipherSuite: 0xC02F);
        var success = TlsServerHelloParser.TryParseServerHello(data, out var info);

        Assert.True(success);
        Assert.NotEmpty(info.Ja3sHash);
        Assert.Equal(32, info.Ja3sHash.Length); // MD5 hex
        Assert.NotEmpty(info.Ja3sRaw);
    }

    [Fact]
    public void TryParseServerHello_WithExtensions_ExtractsThem()
    {
        var data = BuildServerHello(extensions: [0xFF01, 0x0017]); // renegotiation_info, extended_master_secret
        var success = TlsServerHelloParser.TryParseServerHello(data, out var info);

        Assert.True(success);
        Assert.Equal(2, info.Extensions.Count);
        Assert.Contains((ushort)0xFF01, info.Extensions);
        Assert.Contains((ushort)0x0017, info.Extensions);
    }

    [Fact]
    public void TryParseServerHello_Tls13SupportedVersions_OverridesVersion()
    {
        // Build a ServerHello with version 0x0303 but supported_versions extension containing 0x0304 (TLS 1.3)
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        using var hsMs = new MemoryStream();
        using var hs = new BinaryWriter(hsMs);

        hs.Write((byte)0x03); hs.Write((byte)0x03); // TLS 1.2 as wire version
        hs.Write(new byte[32]); // Random
        hs.Write((byte)0); // Session ID len
        hs.Write((byte)0xC0); hs.Write((byte)0x2F); // cipher suite
        hs.Write((byte)0); // compression

        // Extensions with supported_versions (0x002B)
        using var extMs = new MemoryStream();
        using var extBw = new BinaryWriter(extMs);
        extBw.Write((byte)0x00); extBw.Write((byte)0x2B); // supported_versions
        extBw.Write((byte)0x00); extBw.Write((byte)0x02); // extension length = 2
        extBw.Write((byte)0x03); extBw.Write((byte)0x04); // TLS 1.3

        var extBytes = extMs.ToArray();
        hs.Write((byte)(extBytes.Length >> 8));
        hs.Write((byte)extBytes.Length);
        hs.Write(extBytes);

        var hsBody = hsMs.ToArray();

        bw.Write((byte)0x16);
        bw.Write((byte)0x03); bw.Write((byte)0x03);
        var recordLen = 4 + hsBody.Length;
        bw.Write((byte)(recordLen >> 8));
        bw.Write((byte)recordLen);
        bw.Write((byte)0x02);
        bw.Write((byte)(hsBody.Length >> 16));
        bw.Write((byte)(hsBody.Length >> 8));
        bw.Write((byte)hsBody.Length);
        bw.Write(hsBody);

        var data = ms.ToArray();
        var success = TlsServerHelloParser.TryParseServerHello(data, out var info);

        Assert.True(success);
        Assert.Equal(0x0304, info.TlsVersion); // Should be TLS 1.3
    }

    [Fact]
    public void TryParseServerHello_TooShort_ReturnsFalse()
    {
        var success = TlsServerHelloParser.TryParseServerHello(new byte[] { 0x16, 0x03 }, out _);
        Assert.False(success);
    }

    [Fact]
    public void TryParseServerHello_NonHandshake_ReturnsFalse()
    {
        var success = TlsServerHelloParser.TryParseServerHello(new byte[] { 0x17, 0x03, 0x03, 0x00, 0x01, 0x00 }, out _);
        Assert.False(success);
    }

    [Fact]
    public void TryParseServerHello_WrongHandshakeType_ReturnsFalse()
    {
        // Build a valid record but with handshake type = ClientHello (1) instead of ServerHello (2)
        var data = BuildServerHello();
        // Patch handshake type from 0x02 to 0x01
        data[5] = 0x01;
        var success = TlsServerHelloParser.TryParseServerHello(data, out _);
        Assert.False(success);
    }

    [Fact]
    public void GetRecordLength_ValidRecord_ReturnsCorrectLength()
    {
        var data = BuildServerHello();
        var len = TlsServerHelloParser.GetRecordLength(data);
        Assert.True(len > 5);
        Assert.True(len <= data.Length);
    }

    [Fact]
    public void GetHandshakeType_ValidServerHello_Returns2()
    {
        var data = BuildServerHello();
        Assert.Equal(0x02, TlsServerHelloParser.GetHandshakeType(data));
    }

    [Fact]
    public void GetHandshakeType_TooShort_Returns0()
    {
        Assert.Equal(0, TlsServerHelloParser.GetHandshakeType(new byte[] { 0x16, 0x03 }));
    }
}
