using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace IoTSpy.Proxy.Tls;

/// <summary>
/// Parses TLS ServerHello and Certificate messages from raw bytes.
/// Extracts selected cipher suite, TLS version, server extensions, and certificate details.
/// </summary>
public static class TlsServerHelloParser
{
    private static readonly HashSet<ushort> GreaseValues =
    [
        0x0A0A, 0x1A1A, 0x2A2A, 0x3A3A, 0x4A4A, 0x5A5A, 0x6A6A, 0x7A7A,
        0x8A8A, 0x9A9A, 0xAAAA, 0xBABA, 0xCACA, 0xDADA, 0xEAEA, 0xFAFA
    ];

    /// <summary>
    /// Attempts to parse a TLS ServerHello from a buffer starting with a TLS record header.
    /// </summary>
    public static bool TryParseServerHello(ReadOnlySpan<byte> data, out ServerHelloInfo info)
    {
        info = default!;

        if (data.Length < 5) return false;
        if (data[0] != 0x16) return false; // Handshake

        var recordLength = BinaryPrimitives.ReadUInt16BigEndian(data[3..]);
        if (data.Length < 5 + recordLength) return false;

        var handshake = data[5..];
        if (handshake.Length < 4) return false;
        if (handshake[0] != 0x02) return false; // ServerHello

        var hsLength = (handshake[1] << 16) | (handshake[2] << 8) | handshake[3];
        if (handshake.Length < 4 + hsLength) return false;

        var hello = handshake[4..];
        var pos = 0;

        // ServerVersion
        if (pos + 2 > hello.Length) return false;
        var serverVersion = BinaryPrimitives.ReadUInt16BigEndian(hello[pos..]);
        pos += 2;

        // Random (32 bytes)
        pos += 32;
        if (pos > hello.Length) return false;

        // SessionID
        if (pos + 1 > hello.Length) return false;
        var sessionIdLen = hello[pos++];
        pos += sessionIdLen;
        if (pos > hello.Length) return false;

        // Cipher suite (2 bytes)
        if (pos + 2 > hello.Length) return false;
        var cipherSuite = BinaryPrimitives.ReadUInt16BigEndian(hello[pos..]);
        pos += 2;

        // Compression method (1 byte)
        if (pos + 1 > hello.Length) return false;
        pos += 1;

        // Extensions
        var extensions = new List<ushort>();
        if (pos + 2 <= hello.Length)
        {
            var extTotalLen = BinaryPrimitives.ReadUInt16BigEndian(hello[pos..]);
            pos += 2;
            var extEnd = pos + extTotalLen;
            if (extEnd > hello.Length) extEnd = hello.Length;

            while (pos + 4 <= extEnd)
            {
                var extType = BinaryPrimitives.ReadUInt16BigEndian(hello[pos..]);
                var extLen = BinaryPrimitives.ReadUInt16BigEndian(hello[(pos + 2)..]);
                pos += 4;
                extensions.Add(extType);

                // Check supported_versions extension for actual TLS version
                if (extType == 0x002B && extLen >= 2) // supported_versions
                {
                    var realVersion = BinaryPrimitives.ReadUInt16BigEndian(hello[pos..]);
                    serverVersion = realVersion;
                }

                pos += extLen;
            }
        }

        // Compute JA3S: TLSVersion,Cipher,Extensions
        var ja3sExtensions = string.Join("-", extensions.Where(e => !GreaseValues.Contains(e)));
        var ja3sRaw = $"{serverVersion},{cipherSuite},{ja3sExtensions}";
        var ja3sHash = ComputeMd5(ja3sRaw);

        info = new ServerHelloInfo
        {
            TlsVersion = serverVersion,
            CipherSuite = cipherSuite,
            Extensions = extensions,
            Ja3sRaw = ja3sRaw,
            Ja3sHash = ja3sHash
        };

        return true;
    }

    /// <summary>
    /// Attempts to parse a TLS Certificate handshake message from a buffer starting with a TLS record header.
    /// Extracts the leaf certificate details.
    /// </summary>
    public static bool TryParseCertificate(ReadOnlySpan<byte> data, out ServerCertificateInfo certInfo)
    {
        certInfo = default!;

        if (data.Length < 5) return false;
        if (data[0] != 0x16) return false; // Handshake

        var recordLength = BinaryPrimitives.ReadUInt16BigEndian(data[3..]);
        if (data.Length < 5 + recordLength) return false;

        var handshake = data[5..];
        if (handshake.Length < 4) return false;
        if (handshake[0] != 0x0B) return false; // Certificate

        var hello = handshake[4..];
        var pos = 0;

        // Certificates total length (3 bytes)
        if (pos + 3 > hello.Length) return false;
        pos += 3;

        // First certificate length (3 bytes) — the leaf cert
        if (pos + 3 > hello.Length) return false;
        var certLen = (hello[pos] << 16) | (hello[pos + 1] << 8) | hello[pos + 2];
        pos += 3;

        if (pos + certLen > hello.Length) return false;
        var certBytes = hello.Slice(pos, certLen);

        try
        {
            using var cert = X509CertificateLoader.LoadCertificate(certBytes);
            var sanList = new List<string>();

            // Extract Subject Alternative Names
            foreach (var ext in cert.Extensions)
            {
                if (ext.Oid?.Value == "2.5.29.17") // Subject Alternative Name
                {
                    var sanStr = ext.Format(multiLine: false);
                    if (!string.IsNullOrEmpty(sanStr))
                    {
                        foreach (var part in sanStr.Split(',', StringSplitOptions.TrimEntries))
                        {
                            var val = part;
                            if (val.StartsWith("DNS Name=", StringComparison.OrdinalIgnoreCase))
                                val = val[9..];
                            else if (val.StartsWith("IP Address=", StringComparison.OrdinalIgnoreCase))
                                val = val[11..];
                            sanList.Add(val);
                        }
                    }
                }
            }

            var sha256 = SHA256.HashData(cert.RawData);

            certInfo = new ServerCertificateInfo
            {
                Subject = cert.Subject,
                Issuer = cert.Issuer,
                SerialNumber = cert.SerialNumber,
                SanList = sanList,
                NotBefore = new DateTimeOffset(cert.NotBefore, TimeSpan.Zero),
                NotAfter = new DateTimeOffset(cert.NotAfter, TimeSpan.Zero),
                Sha256Fingerprint = Convert.ToHexStringLower(sha256)
            };
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Determines the total length of the TLS record at the start of the buffer.
    /// Returns 0 if insufficient data.
    /// </summary>
    public static int GetRecordLength(ReadOnlySpan<byte> data)
    {
        if (data.Length < 5) return 0;
        if (data[0] != 0x16) return 0;
        return 5 + BinaryPrimitives.ReadUInt16BigEndian(data[3..]);
    }

    /// <summary>
    /// Returns the TLS handshake message type in this record, or 0 if unreadable.
    /// </summary>
    public static byte GetHandshakeType(ReadOnlySpan<byte> data)
    {
        if (data.Length < 6) return 0;
        if (data[0] != 0x16) return 0;
        return data[5];
    }

    private static string ComputeMd5(string input)
    {
        var hash = MD5.HashData(Encoding.ASCII.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }
}

public record ServerHelloInfo
{
    public ushort TlsVersion { get; init; }
    public ushort CipherSuite { get; init; }
    public List<ushort> Extensions { get; init; } = [];
    public string Ja3sRaw { get; init; } = string.Empty;
    public string Ja3sHash { get; init; } = string.Empty;
}

public record ServerCertificateInfo
{
    public string Subject { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public List<string> SanList { get; init; } = [];
    public DateTimeOffset? NotBefore { get; init; }
    public DateTimeOffset? NotAfter { get; init; }
    public string Sha256Fingerprint { get; init; } = string.Empty;
}
