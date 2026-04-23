using System.Globalization;

namespace IoTSpy.Manipulation.ApiSpec;

/// <summary>
/// Parses HTTP <c>Range:</c> request headers into concrete byte offsets.
/// Only single-range forms are supported — multi-range (comma-separated) requests
/// return false so the caller falls back to a full 200 OK response.
/// </summary>
public static class RangeHelper
{
    public readonly record struct ByteRange(long Start, long End, long TotalLength)
    {
        public long Length => End - Start + 1;
    }

    /// <summary>
    /// Parse a <c>Range:</c> header against a known file length. Supports:
    /// <list type="bullet">
    /// <item><c>bytes=S-E</c> — explicit start/end</item>
    /// <item><c>bytes=S-</c>  — from S to end of file</item>
    /// <item><c>bytes=-N</c>  — last N bytes</item>
    /// </list>
    /// Returns false for unsatisfiable or multi-range requests.
    /// </summary>
    public static bool TryParse(string? header, long fileLength, out ByteRange range)
    {
        range = default;
        if (string.IsNullOrWhiteSpace(header) || fileLength <= 0) return false;

        // "bytes=..." prefix
        var eq = header.IndexOf('=');
        if (eq <= 0) return false;
        var unit = header[..eq].Trim();
        if (!unit.Equals("bytes", StringComparison.OrdinalIgnoreCase)) return false;

        var spec = header[(eq + 1)..].Trim();
        if (spec.Contains(',')) return false; // multi-range not supported

        var dash = spec.IndexOf('-');
        if (dash < 0) return false;

        var startPart = spec[..dash].Trim();
        var endPart = spec[(dash + 1)..].Trim();

        long start, end;
        if (startPart.Length == 0)
        {
            // Suffix form: last N bytes.
            if (!long.TryParse(endPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var suffix) || suffix <= 0)
                return false;
            if (suffix > fileLength) suffix = fileLength;
            start = fileLength - suffix;
            end = fileLength - 1;
        }
        else
        {
            if (!long.TryParse(startPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out start) || start < 0)
                return false;
            if (endPart.Length == 0)
            {
                end = fileLength - 1;
            }
            else if (!long.TryParse(endPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out end) || end < start)
            {
                return false;
            }
            if (end >= fileLength) end = fileLength - 1;
            if (start >= fileLength) return false;
        }

        range = new ByteRange(start, end, fileLength);
        return true;
    }
}
