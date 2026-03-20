using IoTSpy.Core.Interfaces;
using Microsoft.Extensions.Options;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IoTSpy.Api.Services;

public sealed class AlertingService : IAlertingService
{
    private readonly AlertingOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AlertingService> _logger;

    public AlertingService(
        IOptions<AlertingOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<AlertingService> logger)
    {
        _options = options.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task SendAlertAsync(string title, string body, AlertSeverity severity, CancellationToken ct = default)
    {
        if (!_options.Enabled) return;

        var tasks = new List<Task>();

        if (_options.Webhook?.Url is { Length: > 0 } webhookUrl)
            tasks.Add(SendWebhookAsync(webhookUrl, title, body, severity, ct));

        if (_options.Email is { SmtpHost: { Length: > 0 } } emailOpts)
            tasks.Add(SendEmailAsync(emailOpts, title, body, severity));

        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }

    private async Task SendWebhookAsync(string url, string title, string body, AlertSeverity severity, CancellationToken ct)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                title,
                body,
                severity = severity.ToString(),
                timestamp = DateTimeOffset.UtcNow
            });

            var client = _httpClientFactory.CreateClient("alerting");
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            if (_options.Webhook?.Secret is { Length: > 0 } secret)
            {
                var sig = ComputeHmac(secret, payload);
                request.Headers.Add("X-IoTSpy-Signature", sig);
            }

            var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("Webhook alert returned {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send webhook alert");
        }
    }

    private async Task SendEmailAsync(EmailOptions opts, string title, string body, AlertSeverity severity)
    {
        try
        {
            using var smtp = new SmtpClient(opts.SmtpHost, opts.SmtpPort);
            if (opts.Username is { Length: > 0 })
                smtp.Credentials = new System.Net.NetworkCredential(opts.Username, opts.Password);
            smtp.EnableSsl = opts.SmtpPort != 25;

            var mail = new MailMessage(opts.From ?? "iotspy@localhost", opts.To ?? "admin@localhost")
            {
                Subject = $"[IoTSpy] [{severity}] {title}",
                Body = body,
                IsBodyHtml = false
            };

            await smtp.SendMailAsync(mail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email alert");
        }
    }

    private static string ComputeHmac(string secret, string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(keyBytes, payloadBytes);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
