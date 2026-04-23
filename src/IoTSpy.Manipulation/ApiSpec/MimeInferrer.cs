namespace IoTSpy.Manipulation.ApiSpec;

/// <summary>
/// Infers a Content-Type from a file extension for the common media types
/// supported by the content replacement engine. Callers can always override
/// via <see cref="Core.Models.ContentReplacementRule.ReplacementContentType"/>.
/// </summary>
internal static class MimeInferrer
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",
        [".mp4"] = "video/mp4",
        [".webm"] = "video/webm",
        [".mov"] = "video/quicktime",
        [".mkv"] = "video/x-matroska",
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".ogg"] = "audio/ogg",
        [".m4a"] = "audio/mp4",
        [".flac"] = "audio/flac",
        [".pdf"] = "application/pdf",
        [".zip"] = "application/zip",
        [".json"] = "application/json",
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".txt"] = "text/plain",
        [".css"] = "text/css",
        [".js"] = "application/javascript",
        [".xml"] = "application/xml",
        [".sse"] = "text/event-stream",
        [".ndjson"] = "application/x-ndjson",
    };

    public static string FromPath(string path)
    {
        var ext = Path.GetExtension(path);
        return Map.TryGetValue(ext, out var mime) ? mime : "application/octet-stream";
    }

    public static bool IsBinary(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        var ct = contentType.Split(';')[0].Trim();
        return ct.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            || ct.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            || ct.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
            || ct.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
            || ct.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            || ct.Equals("application/zip", StringComparison.OrdinalIgnoreCase);
    }
}
