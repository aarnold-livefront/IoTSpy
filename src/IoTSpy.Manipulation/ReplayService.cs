using System.Diagnostics;
using System.Text;
using IoTSpy.Core.Models;
using Microsoft.Extensions.Logging;

namespace IoTSpy.Manipulation;

/// <summary>
/// Replays captured HTTP requests (optionally with modifications) and records the response.
/// </summary>
public class ReplayService(IHttpClientFactory httpClientFactory, ILogger<ReplayService> logger)
{
    public async Task<ReplaySession> ExecuteReplayAsync(ReplaySession session, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var scheme = session.RequestScheme.ToLowerInvariant();
            var port = session.RequestPort;
            var includePort = (scheme == "http" && port != 80) || (scheme == "https" && port != 443);
            var authority = includePort ? $"{session.RequestHost}:{port}" : session.RequestHost;

            var uriBuilder = new UriBuilder(scheme, session.RequestHost, port, session.RequestPath)
            {
                Query = session.RequestQuery
            };

            using var request = new HttpRequestMessage(new HttpMethod(session.RequestMethod), uriBuilder.Uri);

            // Apply custom headers
            if (!string.IsNullOrEmpty(session.RequestHeaders))
            {
                foreach (var line in session.RequestHeaders.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
                {
                    var colonIdx = line.IndexOf(':');
                    if (colonIdx <= 0) continue;
                    var name = line[..colonIdx].Trim();
                    var value = line[(colonIdx + 1)..].Trim();

                    // Skip restricted headers that HttpClient manages
                    if (name.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        // Set via content headers instead
                        continue;
                    }

                    request.Headers.TryAddWithoutValidation(name, value);
                }
            }

            if (!string.IsNullOrEmpty(session.RequestBody))
            {
                var contentType = ExtractHeaderValue(session.RequestHeaders, "Content-Type") ?? "application/octet-stream";
                request.Content = new StringContent(session.RequestBody, Encoding.UTF8, contentType);
            }

            var client = httpClientFactory.CreateClient("IoTSpyReplay");
            using var response = await client.SendAsync(request, ct);

            session.ResponseStatusCode = (int)response.StatusCode;
            session.ResponseBody = await response.Content.ReadAsStringAsync(ct);

            var respHeaderSb = new StringBuilder();
            foreach (var header in response.Headers.Concat(response.Content.Headers))
                respHeaderSb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
            session.ResponseHeaders = respHeaderSb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Replay request failed for session {SessionId}", session.Id);
            session.ResponseStatusCode = 0;
            session.ResponseBody = $"Error: {ex.Message}";
        }

        sw.Stop();
        session.DurationMs = sw.ElapsedMilliseconds;

        return session;
    }

    private static string? ExtractHeaderValue(string headers, string name)
    {
        foreach (var line in headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase))
                return line[(name.Length + 1)..].Trim();
        }
        return null;
    }
}
