using IoTSpy.Api.Controllers;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using System.Text;
using System.Text.Json;
using Xunit;

namespace IoTSpy.Api.Tests.Controllers;

public class RulesetBundleExportTests
{
    private static (ManipulationController controller, IManipulationRuleRepository rules,
        IBreakpointRepository breakpoints, IApiSpecRepository apiSpecs) MakeController()
    {
        var rules = Substitute.For<IManipulationRuleRepository>();
        var breakpoints = Substitute.For<IBreakpointRepository>();
        var apiSpecs = Substitute.For<IApiSpecRepository>();

        rules.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<ManipulationRule>());
        breakpoints.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Breakpoint>());
        apiSpecs.GetAllStandaloneRulesAsync(Arg.Any<CancellationToken>()).Returns(new List<ContentReplacementRule>());
        apiSpecs.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<ApiSpecDocument>());

        var controller = new ManipulationController(
            Substitute.For<IManipulationService>(),
            rules,
            breakpoints,
            Substitute.For<IReplaySessionRepository>(),
            Substitute.For<IFuzzerJobRepository>(),
            Substitute.For<ICaptureRepository>(),
            apiSpecs);

        return (controller, rules, breakpoints, apiSpecs);
    }

    [Fact]
    public async Task ExportRuleset_ReturnsJsonFileWithFilename()
    {
        var (controller, _, _, _) = MakeController();
        var result = await controller.ExportRuleset(null, CancellationToken.None) as FileContentResult;

        Assert.NotNull(result);
        Assert.Equal("application/json", result.ContentType);
        Assert.Equal("ruleset.json", result.FileDownloadName);
    }

    [Fact]
    public async Task ExportRuleset_IncludesAllTopLevelKeys()
    {
        var (controller, _, _, _) = MakeController();
        var result = await controller.ExportRuleset(null, CancellationToken.None) as FileContentResult;

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(Encoding.UTF8.GetString(result.FileContents));
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("exportedAt", out _));
        Assert.True(root.TryGetProperty("trafficRules", out _));
        Assert.True(root.TryGetProperty("breakpoints", out _));
        Assert.True(root.TryGetProperty("contentReplacementRules", out _));
        Assert.True(root.TryGetProperty("apiSpecs", out _));
        Assert.True(root.TryGetProperty("referencedAssets", out _));
    }

    [Fact]
    public async Task ExportRuleset_IncludesTrafficRulesAndBreakpoints()
    {
        var (controller, rules, breakpoints, _) = MakeController();
        rules.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<ManipulationRule>
        {
            new() { Name = "Block ads", Action = ManipulationRuleAction.Drop }
        });
        breakpoints.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Breakpoint>
        {
            new() { Name = "Debug breakpoint", Language = ScriptLanguage.JavaScript, ScriptCode = "return true;" }
        });

        var result = await controller.ExportRuleset(null, CancellationToken.None) as FileContentResult;

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(Encoding.UTF8.GetString(result.FileContents));
        Assert.Equal(1, doc.RootElement.GetProperty("trafficRules").GetArrayLength());
        Assert.Equal(1, doc.RootElement.GetProperty("breakpoints").GetArrayLength());
    }

    [Fact]
    public async Task ExportRuleset_ReferencedAssets_ListsUniqueFilenames()
    {
        var (controller, _, _, apiSpecs) = MakeController();
        apiSpecs.GetAllStandaloneRulesAsync(Arg.Any<CancellationToken>()).Returns(new List<ContentReplacementRule>
        {
            new() { Name = "Rule A", ReplacementFilePath = "/data/assets/stream_abc.sse" },
            new() { Name = "Rule B", ReplacementFilePath = "/data/assets/stream_abc.sse" },
            new() { Name = "Rule C", ReplacementFilePath = "/data/assets/events_xyz.ndjson" },
            new() { Name = "Rule D", ReplacementFilePath = null }
        });

        var result = await controller.ExportRuleset(null, CancellationToken.None) as FileContentResult;

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(Encoding.UTF8.GetString(result.FileContents));
        var assets = doc.RootElement.GetProperty("referencedAssets");
        Assert.Equal(2, assets.GetArrayLength());
    }

    [Fact]
    public async Task ExportRuleset_WithSpecIdFilter_OnlyIncludesThatSpec()
    {
        var specId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var (controller, _, _, apiSpecs) = MakeController();

        apiSpecs.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<ApiSpecDocument>
        {
            new() { Id = specId, Name = "Target spec", Host = "api.example.com" },
            new() { Id = otherId, Name = "Other spec", Host = "other.example.com" }
        });
        apiSpecs.GetReplacementRulesAsync(specId, Arg.Any<CancellationToken>()).Returns(new List<ContentReplacementRule>());

        var result = await controller.ExportRuleset(specId, CancellationToken.None) as FileContentResult;

        Assert.NotNull(result);
        var doc = JsonDocument.Parse(Encoding.UTF8.GetString(result.FileContents));
        Assert.Equal(1, doc.RootElement.GetProperty("apiSpecs").GetArrayLength());
        await apiSpecs.DidNotReceive().GetReplacementRulesAsync(otherId, Arg.Any<CancellationToken>());
    }
}
