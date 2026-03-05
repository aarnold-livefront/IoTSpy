using System.Text;
using IoTSpy.Api.Hubs;
using IoTSpy.Api.Services;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Proxy;
using IoTSpy.Proxy.Interception;
using IoTSpy.Proxy.Resilience;
using IoTSpy.Proxy.Tls;
using IoTSpy.Manipulation;
using IoTSpy.Scanner;
using IoTSpy.Storage.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Storage ─────────────────────────────────────────────────────────────────
var dbProvider = builder.Configuration["Database:Provider"] ?? "sqlite";
var connString = builder.Configuration["Database:ConnectionString"]
    ?? $"Data Source={Path.Combine(AppContext.BaseDirectory, "iotspy.db")}";
builder.Services.AddIoTSpyStorage(connString, dbProvider);

// ── Authentication ───────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Auth:JwtSecret"]
    ?? throw new InvalidOperationException("Auth:JwtSecret must be set in configuration.");

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

// ── Resilience ───────────────────────────────────────────────────────────────
var resilienceOptions = builder.Configuration
    .GetSection(ResilienceOptions.SectionName)
    .Get<ResilienceOptions>() ?? new ResilienceOptions();
builder.Services.AddProxyResilience(resilienceOptions);

// ── Proxy ────────────────────────────────────────────────────────────────────
// All proxy servers and supporting engines are singletons (long-lived TCP listeners)
builder.Services.AddSingleton<ExplicitProxyServer>();
builder.Services.AddSingleton<TransparentProxyServer>();
builder.Services.AddSingleton<IptablesHelper>();
builder.Services.AddSingleton<ArpSpoofEngine>();
builder.Services.AddSingleton<ICertificateAuthority, CertificateAuthority>();
builder.Services.AddSingleton<IProxyService, ProxyService>();
builder.Services.AddHostedService(sp => (ProxyService)sp.GetRequiredService<IProxyService>());

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

// ── CORS (for Vinext dev server) ─────────────────────────────────────────────
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

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<TrafficHub>("/hubs/traffic");

// SPA fallback — any unmatched route serves index.html for client-side routing
app.MapFallbackToFile("index.html");

app.Run();
