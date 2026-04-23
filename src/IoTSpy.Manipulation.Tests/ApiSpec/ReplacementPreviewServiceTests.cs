using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Manipulation.ApiSpec;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IoTSpy.Manipulation.Tests.ApiSpec;

public class ReplacementPreviewServiceTests
{
    private static (ReplacementPreviewService Svc, IApiSpecRepository SpecRepo, ICaptureRepository CaptureRepo) Create()
    {
        var specRepo = Substitute.For<IApiSpecRepository>();
        var captureRepo = Substitute.For<ICaptureRepository>();
        var replacer = new ContentReplacer(NullLogger<ContentReplacer>.Instance);
        var svc = new ReplacementPreviewService(
            replacer, specRepo, captureRepo,
            NullLogger<ReplacementPreviewService>.Instance);
        return (svc, specRepo, captureRepo);
    }

    [Fact]
    public async Task UnknownRule_ReturnsNull()
    {
        var (svc, specRepo, _) = Create();
        var specId = Guid.NewGuid();
        specRepo.GetReplacementRulesAsync(specId, Arg.Any<CancellationToken>())
            .Returns(new List<ContentReplacementRule>());

        var result = await svc.PreviewAsync(specId, Guid.NewGuid(),
            new PreviewRequest(Synthetic: new SyntheticMessage()),
            TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task Synthetic_AppliesRuleAndReturnsModifiedText()
    {
        var (svc, specRepo, _) = Create();
        var specId = Guid.NewGuid();
        var rule = new ContentReplacementRule
        {
            Id = Guid.NewGuid(),
            Enabled = true,
            MatchType = ContentMatchType.BodyRegex,
            MatchPattern = "secret",
            Action = ContentReplacementAction.ReplaceWithValue,
            ReplacementValue = "REDACTED",
        };
        specRepo.GetReplacementRulesAsync(specId, Arg.Any<CancellationToken>())
            .Returns([rule]);

        var req = new PreviewRequest(Synthetic: new SyntheticMessage(
            ResponseHeaders: new Dictionary<string, string> { ["Content-Type"] = "text/plain" },
            ResponseBody: "the secret is out"));

        var result = await svc.PreviewAsync(specId, rule.Id, req, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.True(result!.Modified);
        Assert.Equal("the REDACTED is out", result.ResponseBodyText);
        Assert.False(result.WasStreamed);
        Assert.Contains("Content-Type", result.ResponseHeaders.Keys);
    }

    [Fact]
    public async Task TrackingPixel_ReturnsGifBase64()
    {
        var (svc, specRepo, _) = Create();
        var specId = Guid.NewGuid();
        var rule = new ContentReplacementRule
        {
            Id = Guid.NewGuid(),
            Enabled = true,
            MatchType = ContentMatchType.ContentType,
            MatchPattern = "image/*",
            Action = ContentReplacementAction.TrackingPixel,
        };
        specRepo.GetReplacementRulesAsync(specId, Arg.Any<CancellationToken>())
            .Returns([rule]);

        var req = new PreviewRequest(Synthetic: new SyntheticMessage(
            ResponseHeaders: new Dictionary<string, string> { ["Content-Type"] = "image/gif" }));

        var result = await svc.PreviewAsync(specId, rule.Id, req, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal("image/gif", result!.ContentType);
        Assert.Equal(43, result.BodyLength);
        Assert.NotNull(result.ResponseBodyBase64);
        // GIF89a magic bytes at start.
        var bytes = Convert.FromBase64String(result.ResponseBodyBase64!);
        Assert.Equal(0x47, bytes[0]);
        Assert.Equal(0x49, bytes[1]);
        Assert.Equal(0x46, bytes[2]);
    }

    [Fact]
    public async Task MissingInput_ReturnsWarning()
    {
        var (svc, specRepo, _) = Create();
        var specId = Guid.NewGuid();
        var rule = new ContentReplacementRule
        {
            Id = Guid.NewGuid(),
            Enabled = true,
            MatchType = ContentMatchType.BodyRegex,
            MatchPattern = "x",
            Action = ContentReplacementAction.Redact,
        };
        specRepo.GetReplacementRulesAsync(specId, Arg.Any<CancellationToken>())
            .Returns([rule]);

        var result = await svc.PreviewAsync(specId, rule.Id,
            new PreviewRequest(),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.False(result!.Modified);
        Assert.Contains(result.Warnings,
            w => w.Contains("captureId or synthetic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CaptureId_NotFound_WarnsAndContinues()
    {
        var (svc, specRepo, captureRepo) = Create();
        var specId = Guid.NewGuid();
        var captureId = Guid.NewGuid();
        var rule = new ContentReplacementRule
        {
            Id = Guid.NewGuid(),
            Enabled = true,
            MatchType = ContentMatchType.BodyRegex,
            MatchPattern = "x",
            Action = ContentReplacementAction.Redact,
        };
        specRepo.GetReplacementRulesAsync(specId, Arg.Any<CancellationToken>())
            .Returns([rule]);
        captureRepo.GetByIdAsync(captureId, Arg.Any<CancellationToken>())
            .Returns((CapturedRequest?)null);

        var result = await svc.PreviewAsync(specId, rule.Id,
            new PreviewRequest(CaptureId: captureId),
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Contains(result!.Warnings, w => w.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }
}
