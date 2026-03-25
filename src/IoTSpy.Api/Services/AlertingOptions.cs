namespace IoTSpy.Api.Services;

public class AlertingOptions
{
    public const string SectionName = "Alerting";

    public bool Enabled { get; set; } = false;
    public WebhookOptions? Webhook { get; set; }
    public EmailOptions? Email { get; set; }
}

public class WebhookOptions
{
    public string? Url { get; set; }
    public string? Secret { get; set; }
}

public class EmailOptions
{
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}
