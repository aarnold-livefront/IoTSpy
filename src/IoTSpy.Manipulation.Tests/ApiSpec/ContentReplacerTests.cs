using System.Text.Json;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using IoTSpy.Manipulation.ApiSpec;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IoTSpy.Manipulation.Tests.ApiSpec;

public class ContentReplacerTests
{
    private static ContentReplacer CreateReplacer() => new(NullLogger<ContentReplacer>.Instance);

    private static HttpMessage MakeJsonResponse(string body = """{"name":"test","email":"user@example.com"}""") =>
        new()
        {
            Host = "api.example.com",
            Path = "/api/users",
            Method = "GET",
            StatusCode = 200,
            ResponseHeaders = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json"
            }),
            ResponseBody = body
        };

    private static HttpMessage MakeImageResponse() =>
        new()
        {
            Host = "cdn.example.com",
            Path = "/images/photo.jpg",
            Method = "GET",
            StatusCode = 200,
            ResponseHeaders = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["Content-Type"] = "image/jpeg"
            }),
            ResponseBody = "binary-image-data"
        };

    // ── Content Type Matching ────────────────────────────────────────────────

    [Theory]
    [InlineData("image/*", "image/jpeg", true)]
    [InlineData("image/*", "image/png", true)]
    [InlineData("image/*", "video/mp4", false)]
    [InlineData("video/*", "video/mp4", true)]
    [InlineData("application/json", "application/json", true)]
    [InlineData("application/json", "text/html", false)]
    [InlineData("*/*", "anything/goes", true)]
    [InlineData("", "image/jpeg", false)]
    public void MatchesContentType_ReturnsExpected(string pattern, string contentType, bool expected)
    {
        var result = ContentReplacer.MatchesContentType(pattern, contentType);
        Assert.Equal(expected, result);
    }

    // ── BodyRegex Replacement ────────────────────────────────────────────────

    [Fact]
    public void Apply_BodyRegex_ReplacesMatches()
    {
        var replacer = CreateReplacer();
        var message = MakeJsonResponse("""{"phone":"555-1234","name":"test"}""");

        var rules = new List<ContentReplacementRule>
        {
            new()
            {
                Name = "Redact phones",
                Enabled = true,
                MatchType = ContentMatchType.BodyRegex,
                MatchPattern = @"\d{3}-\d{4}",
                Action = ContentReplacementAction.ReplaceWithValue,
                ReplacementValue = "XXX-XXXX",
                Priority = 0
            }
        };

        var modified = replacer.Apply(message, rules);

        Assert.True(modified);
        Assert.Contains("XXX-XXXX", message.ResponseBody);
        Assert.DoesNotContain("555-1234", message.ResponseBody);
    }

    [Fact]
    public void Apply_BodyRegex_Redact_ReplacesWithRedacted()
    {
        var replacer = CreateReplacer();
        var message = MakeJsonResponse("""{"secret":"abc123"}""");

        var rules = new List<ContentReplacementRule>
        {
            new()
            {
                Name = "Redact secrets",
                Enabled = true,
                MatchType = ContentMatchType.BodyRegex,
                MatchPattern = "abc123",
                Action = ContentReplacementAction.Redact,
                Priority = 0
            }
        };

        var modified = replacer.Apply(message, rules);

        Assert.True(modified);
        Assert.Contains("[REDACTED]", message.ResponseBody);
    }

    // ── JsonPath Replacement ─────────────────────────────────────────────────

    [Fact]
    public void Apply_JsonPath_ReplacesField()
    {
        var replacer = CreateReplacer();
        var message = MakeJsonResponse("""{"user":{"email":"real@test.com","name":"John"}}""");

        var rules = new List<ContentReplacementRule>
        {
            new()
            {
                Name = "Replace email",
                Enabled = true,
                MatchType = ContentMatchType.JsonPath,
                MatchPattern = "$.user.email",
                Action = ContentReplacementAction.ReplaceWithValue,
                ReplacementValue = "fake@example.com",
                Priority = 0
            }
        };

        var modified = replacer.Apply(message, rules);

        Assert.True(modified);
        Assert.Contains("fake@example.com", message.ResponseBody);
        Assert.DoesNotContain("real@test.com", message.ResponseBody);
    }

    [Fact]
    public void Apply_JsonPath_RedactsField()
    {
        var replacer = CreateReplacer();
        var message = MakeJsonResponse("""{"token":"secret-value","data":"public"}""");

        var rules = new List<ContentReplacementRule>
        {
            new()
            {
                Name = "Redact token",
                Enabled = true,
                MatchType = ContentMatchType.JsonPath,
                MatchPattern = "$.token",
                Action = ContentReplacementAction.Redact,
                Priority = 0
            }
        };

        var modified = replacer.Apply(message, rules);

        Assert.True(modified);
        Assert.Contains("[REDACTED]", message.ResponseBody);
        Assert.DoesNotContain("secret-value", message.ResponseBody);
    }

    // ── Header Replacement ───────────────────────────────────────────────────

    [Fact]
    public void Apply_HeaderValue_ReplacesHeader()
    {
        var replacer = CreateReplacer();
        var message = MakeJsonResponse();

        var rules = new List<ContentReplacementRule>
        {
            new()
            {
                Name = "Replace content type",
                Enabled = true,
                MatchType = ContentMatchType.HeaderValue,
                MatchPattern = "Content-Type",
                Action = ContentReplacementAction.ReplaceWithValue,
                ReplacementValue = "text/plain",
                Priority = 0
            }
        };

        var modified = replacer.Apply(message, rules);

        Assert.True(modified);
        Assert.Contains("text/plain", message.ResponseHeaders);
    }

    // ── Content Type Rule ────────────────────────────────────────────────────

    [Fact]
    public void Apply_ContentType_ReplaceWithValue()
    {
        var replacer = CreateReplacer();
        var message = MakeImageResponse();

        var rules = new List<ContentReplacementRule>
        {
            new()
            {
                Name = "Replace images",
                Enabled = true,
                MatchType = ContentMatchType.ContentType,
                MatchPattern = "image/*",
                Action = ContentReplacementAction.ReplaceWithValue,
                ReplacementValue = "custom-image-data",
                ReplacementContentType = "image/png",
                Priority = 0
            }
        };

        var modified = replacer.Apply(message, rules);

        Assert.True(modified);
        Assert.Equal("custom-image-data", message.ResponseBody);
        Assert.Contains("image/png", message.ResponseHeaders);
    }

    [Fact]
    public void Apply_ContentType_Redact()
    {
        var replacer = CreateReplacer();
        var message = MakeImageResponse();

        var rules = new List<ContentReplacementRule>
        {
            new()
            {
                Name = "Redact images",
                Enabled = true,
                MatchType = ContentMatchType.ContentType,
                MatchPattern = "image/*",
                Action = ContentReplacementAction.Redact,
                Priority = 0
            }
        };

        var modified = replacer.Apply(message, rules);

        Assert.True(modified);
        Assert.Empty(message.ResponseBody);
    }

    // ── Scope Matching ───────────────────────────────────────────────────────

    [Fact]
    public void Apply_HostPatternDoesNotMatch_Skips()
    {
        var replacer = CreateReplacer();
        var message = MakeJsonResponse();

        var rules = new List<ContentReplacementRule>
        {
            new()
            {
                Name = "Wrong host",
                Enabled = true,
                MatchType = ContentMatchType.BodyRegex,
                MatchPattern = "test",
                Action = ContentReplacementAction.Redact,
                HostPattern = "other\\.com",
                Priority = 0
            }
        };

        var modified = replacer.Apply(message, rules);
        Assert.False(modified);
    }

    [Fact]
    public void Apply_PathPatternDoesNotMatch_Skips()
    {
        var replacer = CreateReplacer();
        var message = MakeJsonResponse();

        var rules = new List<ContentReplacementRule>
        {
            new()
            {
                Name = "Wrong path",
                Enabled = true,
                MatchType = ContentMatchType.BodyRegex,
                MatchPattern = "test",
                Action = ContentReplacementAction.Redact,
                PathPattern = "/other/.*",
                Priority = 0
            }
        };

        var modified = replacer.Apply(message, rules);
        Assert.False(modified);
    }

    // ── Disabled Rules ───────────────────────────────────────────────────────

    [Fact]
    public void Apply_DisabledRule_IsSkipped()
    {
        var replacer = CreateReplacer();
        var message = MakeJsonResponse("""{"name":"test"}""");

        var rules = new List<ContentReplacementRule>
        {
            new()
            {
                Name = "Disabled",
                Enabled = false,
                MatchType = ContentMatchType.BodyRegex,
                MatchPattern = "test",
                Action = ContentReplacementAction.Redact,
                Priority = 0
            }
        };

        var modified = replacer.Apply(message, rules);
        Assert.False(modified);
    }

    // ── Priority Order ───────────────────────────────────────────────────────

    [Fact]
    public void Apply_RulesApplyInPriorityOrder()
    {
        var replacer = CreateReplacer();
        var message = MakeJsonResponse("""{"value":"original"}""");

        var rules = new List<ContentReplacementRule>
        {
            new()
            {
                Name = "Second",
                Enabled = true,
                MatchType = ContentMatchType.BodyRegex,
                MatchPattern = "first-replace",
                Action = ContentReplacementAction.ReplaceWithValue,
                ReplacementValue = "second-replace",
                Priority = 10
            },
            new()
            {
                Name = "First",
                Enabled = true,
                MatchType = ContentMatchType.BodyRegex,
                MatchPattern = "original",
                Action = ContentReplacementAction.ReplaceWithValue,
                ReplacementValue = "first-replace",
                Priority = 1
            }
        };

        var modified = replacer.Apply(message, rules);

        Assert.True(modified);
        Assert.Contains("second-replace", message.ResponseBody);
    }

    // ── No Match ─────────────────────────────────────────────────────────────

    [Fact]
    public void Apply_NoMatchingRules_ReturnsFalse()
    {
        var replacer = CreateReplacer();
        var message = MakeJsonResponse();

        var rules = new List<ContentReplacementRule>
        {
            new()
            {
                Name = "No match",
                Enabled = true,
                MatchType = ContentMatchType.BodyRegex,
                MatchPattern = "nonexistent-pattern-xyz",
                Action = ContentReplacementAction.Redact,
                Priority = 0
            }
        };

        var modified = replacer.Apply(message, rules);
        Assert.False(modified);
    }

    [Fact]
    public void Apply_EmptyRules_ReturnsFalse()
    {
        var replacer = CreateReplacer();
        var message = MakeJsonResponse();

        var modified = replacer.Apply(message, []);
        Assert.False(modified);
    }
}
