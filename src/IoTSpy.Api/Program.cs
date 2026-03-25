using System.Text;
using System.Threading.RateLimiting;
using IoTSpy.Api.Hubs;
using IoTSpy.Api.Services;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Protocols.Anomaly;
using IoTSpy.Proxy;
using IoTSpy.Proxy.Interception;
using IoTSpy.Proxy.Resilience;
using IoTSpy.Proxy.Tls;
using IoTSpy.Manipulation;
using IoTSpy.Scanner;
using IoTSpy.Storage.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog (Phase 8.2) ───────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext());

// ── Storage ─────────────────────────────────────────────────────────────────
var dbProvider = builder.Configuration["Database:Provider"] ?? "sqlite";
var connString = builder.Configuration["Database:ConnectionString"]
    ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "iotspy.db")}";
var dbMaxPool = int.TryParse(builder.Configuration["Database:MaxPoolSize"], out var mp) ? mp : 20;
var dbMinPool = int.TryParse(builder.Configuration["Database:MinPoolSize"], out var minp) ? minp : 1;
builder.Services.AddIoTSpyStorage(connString, dbProvider, dbMaxPool, dbMinPool);

// ── Authentication ───────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Auth:JwtSecret"]
    ?? throw new InvalidOperationException("Auth:JwtSecret must be set in configuration.");
if (jwtSecret.Length < 32)
    throw new InvalidOperationException("Auth:JwtSecret must be at least 32 characters (256 bits) for HS256.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "iotspy",
            ValidateAudience = true,
            ValidAudience = "iotspy",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };

        // Allow JWT via SignalR query string
        opts.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// ── SignalR ──────────────────────────────────────────────────────────────────
builder.Services.AddSignalR();
builder.Services.AddSingleton<ICapturePublisher, SignalRCapturePublisher>();
builder.Services.AddSingleton<IPacketCapturePublisher, SignalRPacketPublisher>();
builder.Services.AddSingleton<IAnomalyAlertPublisher, SignalRAnomalyPublisher>();

// ── Resilience ───────────────────────────────────────────────────────────────
var resilienceOptions = builder.Configuration
    .GetSection(ResilienceOptions.SectionName)
    .Get<ResilienceOptions>() ?? new ResilienceOptions();
builder.Services.AddProxyResilience(resilienceOptions);

// ── Proxy ────────────────────────────────────────────────────────────────────
// All proxy servers and supporting engines are singletons (long-lived TCP listeners)
builder.Services.AddSingleton<SslStripService>();
builder.Services.AddSingleton<ExplicitProxyServer>();
builder.Services.AddSingleton<TransparentProxyServer>();
builder.Services.AddSingleton<IptablesHelper>();
builder.Services.AddSingleton<ArpSpoofEngine>();
builder.Services.AddSingleton<ICertificateAuthority, CertificateAuthority>();
builder.Services.AddSingleton<IProxyService, ProxyService>();
builder.Services.AddHostedService(sp => (ProxyService)sp.GetRequiredService<IProxyService>());

// ── Phase 10: Protocol proxies (MQTT broker proxy, CoAP proxy) ─────────────
builder.Services.AddSingleton<IMqttBrokerProxy, MqttBrokerProxy>();
builder.Services.AddSingleton<ICoapProxy, CoapProxy>();

// ── Anomaly detection (Phase 8.5) ─────────────────────────────────────────
builder.Services.AddSingleton<IAnomalyDetector, AnomalyDetector>();

// ── Scanner ─────────────────────────────────────────────────────────────────
builder.Services.AddIoTSpyScanner();

// ── Manipulation ────────────────────────────────────────────────────────────
var aiConfig = builder.Configuration
    .GetSection(AiProviderConfig.SectionName)
    .Get<AiProviderConfig>();
builder.Services.AddIoTSpyManipulation(aiConfig);

// ── API ──────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped<AuthService>();

// ── Health checks (Phase 8.1) ────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy("API is running"), tags: ["ready", "live"])
    .AddDbContextCheck<IoTSpy.Storage.IoTSpyDbContext>("database", tags: ["ready"]);

// ── Rate limiting (Phase 8.3) ─────────────────────────────────────────────────
var rateLimitEnabled = bool.TryParse(builder.Configuration["RateLimit:Enabled"], out var rle) && rle;
if (rateLimitEnabled)
{
    var permitLimit = int.TryParse(builder.Configuration["RateLimit:PermitLimit"], out var pl) ? pl : 100;
    var windowSeconds = int.TryParse(builder.Configuration["RateLimit:WindowSeconds"], out var ws) ? ws : 60;
    var queueLimit = int.TryParse(builder.Configuration["RateLimit:QueueLimit"], out var ql) ? ql : 10;

    builder.Services.AddRateLimiter(opts =>
    {
        opts.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        {
            var key = ctx.User?.Identity?.Name
                ?? ctx.Connection.RemoteIpAddress?.ToString()
                ?? "anonymous";
            return RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                SegmentsPerWindow = 4,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = queueLimit
            });
        });
        opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });
}

// ── Data retention (Phase 8.4) ────────────────────────────────────────────────
builder.Services.Configure<DataRetentionOptions>(
    builder.Configuration.GetSection(DataRetentionOptions.SectionName));
builder.Services.AddHostedService<DataRetentionService>();

// ── Alerting (Phase 9.4) ──────────────────────────────────────────────────────
builder.Services.Configure<AlertingOptions>(
    builder.Configuration.GetSection(AlertingOptions.SectionName));
builder.Services.AddHttpClient("alerting");
builder.Services.AddSingleton<IAlertingService, AlertingService>();

// ── Scheduled Scans (Phase 9.5) ───────────────────────────────────────────────
builder.Services.AddHostedService<ScheduledScanService>();

// ── CORS (for Vite dev server) ─────────────────────────────────────────────
builder.Services.AddCors(opts => opts.AddDefaultPolicy(policy =>
    policy.WithOrigins(
            builder.Configuration["Frontend:Origin"] ?? "http://localhost:3000")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var app = builder.Build();

// ── Migrate DB on startup ────────────────────────────────────────────────────
await app.Services.MigrateAsync();

// ── Middleware ────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Serve the Vite-built frontend from wwwroot (production / Docker)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diag, ctx) =>
    {
        diag.Set("RequestHost", ctx.Request.Host.Value ?? string.Empty);
        diag.Set("UserAgent", (object)(ctx.Request.Headers.UserAgent.FirstOrDefault() ?? string.Empty));
    };
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (rateLimitEnabled)
    app.UseRateLimiter();

app.MapControllers();
app.MapHub<TrafficHub>("/hubs/traffic");
app.MapHub<PacketCaptureHub>("/hubs/packets");

// ── Health check endpoints (Phase 8.1) ───────────────────────────────────────
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() })
        });
        await ctx.Response.WriteAsync(result);
    }
});

app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString(), description = e.Value.Description })
        });
        await ctx.Response.WriteAsync(result);
    }
});

// SPA fallback — any unmatched route serves index.html for client-side routing
app.MapFallbackToFile("index.html");

app.Run();

// Expose Program to WebApplicationFactory used in integration test projects
public partial class Program { }
