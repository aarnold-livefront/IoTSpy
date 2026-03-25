using IoTSpy.Proxy.Tls;
using Xunit;

namespace IoTSpy.Proxy.Tests;

public class TlsClientHelloParserTests
{
    /// <summary>
    /// Builds a minimal TLS 1.2 ClientHello record with the given SNI hostname and cipher suites.
    /// </summary>
    private static byte[] BuildClientHello(string sniHostname = "example.com", ushort[]? cipherSuites = null)
    {
        cipherSuites ??= [0x1301, 0x1302, 0xC02F]; // TLS_AES_128_GCM, TLS_AES_256_GCM, TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // We'll build the handshake body first, then wrap with record header
        using var hsMs = new MemoryStream();
        using var hs = new BinaryWriter(hsMs);

        // ClientVersion = TLS 1.2 (0x0303)
        hs.Write((byte)0x03);
        hs.Write((byte)0x03);

        // Random (32 bytes)
        hs.Write(new byte[32]);

        // Session ID (0 length)
        hs.Write((byte)0);

        // Cipher suites
        hs.Write((byte)(cipherSuites.Length * 2 >> 8));
        hs.Write((byte)(cipherSuites.Length * 2));
        foreach (var cs in cipherSuites)
        {
            hs.Write((byte)(cs >> 8));
            hs.Write((byte)cs);
        }

        // Compression methods (1 null)
        hs.Write((byte)1);
        hs.Write((byte)0);

        // Extensions
        var extBytes = BuildSniExtension(sniHostname);
        // Add supported_groups extension (0x000A) with a curve
        var groupsExt = BuildSupportedGroupsExtension([0x0017]); // secp256r1
        // Add ec_point_formats extension (0x000B)
        var ecpfExt = BuildEcPointFormatsExtension([0x00]); // uncompressed

        var totalExtLen = extBytes.Length + groupsExt.Length + ecpfExt.Length;
        hs.Write((byte)(totalExtLen >> 8));
        hs.Write((byte)totalExtLen);
        hs.Write(extBytes);
        hs.Write(groupsExt);
        hs.Write(ecpfExt);

        var hsBody = hsMs.ToArray();

        // Handshake header: type=ClientHello(1), length (3 bytes)
        bw.Write((byte)0x16); // ContentType.Handshake
        bw.Write((byte)0x03); // TLS 1.0 record version
        bw.Write((byte)0x01);
        var recordLen = 4 + hsBody.Length; // handshake header + body
        bw.Write((byte)(recordLen >> 8));
        bw.Write((byte)recordLen);

        // Handshake: type=ClientHello(1), length (3 bytes)
        bw.Write((byte)0x01);
        bw.Write((byte)(hsBody.Length >> 16));
        bw.Write((byte)(hsBody.Length >> 8));
        bw.Write((byte)hsBody.Length);
        bw.Write(hsBody);

        return ms.ToArray();
    }

    private static byte[] BuildSniExtension(string hostname)
    {
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(hostname);
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // Extension type = 0x0000 (SNI)
        bw.Write((byte)0x00);
        bw.Write((byte)0x00);

        // Extension data length
        var sniListLen = 3 + nameBytes.Length; // type(1) + nameLen(2) + name
        var extDataLen = 2 + sniListLen; // listLen(2) + sniList
        bw.Write((byte)(extDataLen >> 8));
        bw.Write((byte)extDataLen);

        // SNI list length
        bw.Write((byte)(sniListLen >> 8));
        bw.Write((byte)sniListLen);

        // SNI entry: type=host_name(0), length, name
        bw.Write((byte)0x00);
        bw.Write((byte)(nameBytes.Length >> 8));
        bw.Write((byte)nameBytes.Length);
        bw.Write(nameBytes);

        return ms.ToArray();
    }

    private static byte[] BuildSupportedGroupsExtension(ushort[] groups)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write((byte)0x00);
        bw.Write((byte)0x0A); // extension type = supported_groups

        var listLen = groups.Length * 2;
        var extDataLen = 2 + listLen;
        bw.Write((byte)(extDataLen >> 8));
        bw.Write((byte)extDataLen);
        bw.Write((byte)(listLen >> 8));
        bw.Write((byte)listLen);
        foreach (var g in groups)
        {
            bw.Write((byte)(g >> 8));
            bw.Write((byte)g);
        }
        return ms.ToArray();
    }

    private static byte[] BuildEcPointFormatsExtension(byte[] formats)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write((byte)0x00);
        bw.Write((byte)0x0B); // extension type = ec_point_formats

        var extDataLen = 1 + formats.Length;
        bw.Write((byte)(extDataLen >> 8));
        bw.Write((byte)extDataLen);
        bw.Write((byte)formats.Length);
        bw.Write(formats);
        return ms.ToArray();
    }

    [Fact]
    public void TryParse_ValidClientHello_ExtractsSniHostname()
    {
        var data = BuildClientHello("api.example.com");
        var success = TlsClientHelloParser.TryParse(data, out var info);

        Assert.True(success);
        Assert.Equal("api.example.com", info.SniHostname);
    }

    [Fact]
    public void TryParse_ValidClientHello_ExtractsCipherSuites()
    {
        var ciphers = new ushort[] { 0x1301, 0xC02F };
        var data = BuildClientHello("example.com", ciphers);
        var success = TlsClientHelloParser.TryParse(data, out var info);

        Assert.True(success);
        Assert.Equal(2, info.CipherSuites.Count);
        Assert.Contains((ushort)0x1301, info.CipherSuites);
        Assert.Contains((ushort)0xC02F, info.CipherSuites);
    }

    [Fact]
    public void TryParse_ValidClientHello_ExtractsExtensions()
    {
        var data = BuildClientHello();
        var success = TlsClientHelloParser.TryParse(data, out var info);

        Assert.True(success);
        Assert.Contains((ushort)0x0000, info.Extensions); // SNI
        Assert.Contains((ushort)0x000A, info.Extensions); // supported_groups
        Assert.Contains((ushort)0x000B, info.Extensions); // ec_point_formats
    }

    [Fact]
    public void TryParse_ValidClientHello_ComputesJa3Hash()
    {
        var data = BuildClientHello();
        var success = TlsClientHelloParser.TryParse(data, out var info);

        Assert.True(success);
        Assert.NotEmpty(info.Ja3Hash);
        Assert.Equal(32, info.Ja3Hash.Length); // MD5 hex = 32 chars
        Assert.NotEmpty(info.Ja3Raw);
        Assert.Contains(",", info.Ja3Raw); // JA3 uses comma-separated fields
    }

    [Fact]
    public void TryParse_ValidClientHello_ExtractsEllipticCurves()
    {
        var data = BuildClientHello();
        var success = TlsClientHelloParser.TryParse(data, out var info);

        Assert.True(success);
        Assert.Contains((ushort)0x0017, info.EllipticCurves); // secp256r1
    }

    [Fact]
    public void TryParse_ValidClientHello_ExtractsEcPointFormats()
    {
        var data = BuildClientHello();
        var success = TlsClientHelloParser.TryParse(data, out var info);

        Assert.True(success);
        Assert.Contains((byte)0x00, info.EcPointFormats); // uncompressed
    }

    [Fact]
    public void TryParse_ValidClientHello_ExtractsTlsVersion()
    {
        var data = BuildClientHello();
        var success = TlsClientHelloParser.TryParse(data, out var info);

        Assert.True(success);
        Assert.Equal(0x0303, info.TlsVersion); // TLS 1.2
    }

    [Fact]
    public void TryParse_GreaseValuesExcludedFromJa3()
    {
        // Include a GREASE cipher suite value
        var ciphers = new ushort[] { 0x0A0A, 0x1301 }; // GREASE + real cipher
        var data = BuildClientHello("example.com", ciphers);
        var success = TlsClientHelloParser.TryParse(data, out var info);

        Assert.True(success);
        // JA3 should not include the GREASE value
        Assert.DoesNotContain("2570", info.Ja3Raw); // 0x0A0A = 2570 decimal
    }

    [Fact]
    public void TryParse_TooShortData_ReturnsFalse()
    {
        var data = new byte[] { 0x16, 0x03, 0x01 }; // Too short
        var success = TlsClientHelloParser.TryParse(data, out _);
        Assert.False(success);
    }

    [Fact]
    public void TryParse_NonHandshakeRecord_ReturnsFalse()
    {
        var data = new byte[] { 0x17, 0x03, 0x03, 0x00, 0x01, 0x00 }; // ContentType = Application Data
        var success = TlsClientHelloParser.TryParse(data, out _);
        Assert.False(success);
    }

    [Fact]
    public void GetRecordLength_ValidRecord_ReturnsCorrectLength()
    {
        var data = BuildClientHello();
        var len = TlsClientHelloParser.GetRecordLength(data);
        Assert.True(len > 5);
        Assert.True(len <= data.Length);
    }

    [Fact]
    public void GetRecordLength_TooShort_ReturnsZero()
    {
        var len = TlsClientHelloParser.GetRecordLength(new byte[] { 0x16, 0x03 });
        Assert.Equal(0, len);
    }

    [Fact]
    public void GetRecordLength_NonHandshake_ReturnsZero()
    {
        var len = TlsClientHelloParser.GetRecordLength(new byte[] { 0x17, 0x03, 0x03, 0x00, 0x05 });
        Assert.Equal(0, len);
    }
}
