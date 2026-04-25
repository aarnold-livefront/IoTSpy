using IoTSpy.Api.Controllers;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using System.Text.Json;
using Xunit;

namespace IoTSpy.Api.Tests.Controllers;

public class RulesetImportTests
{
    private static ManipulationController MakeController(
        IManipulationRuleRepository? rules = null,
        IBreakpointRepository? breakpoints = null,
        IApiSpecRepository? apiSpecs = null)
    {
        var rs = Substitute.For<IReplaySessionRepository>();
        rs.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new List<ReplaySession>());
        rs.CountAsync(Arg.Any<CancellationToken>()).Returns(0);
        var fj = Substitute.For<IFuzzerJobRepository>();
        fj.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new List<FuzzerJob>());
        fj.CountAsync(Arg.Any<CancellationToken>()).Returns(0);

        return new ManipulationController(
            Substitute.For<IManipulationService>(),
            rules ?? Substitute.For<IManipulationRuleRepository>(),
            breakpoints ?? Substitute.For<IBreakpointRepository>(),
            rs, fj,
            Substitute.For<ICaptureRepository>(),
            apiSpecs ?? Substitute.For<IApiSpecRepository>(),
            Substitute.For<IAuditRepository>());
    }

    [Fact]
    public async Task ImportRuleset_ImportsTrafficRulesWithNewIds()
    {
        var originalId = Guid.NewGuid();
        var capturedId = Guid.Empty;

        var rules = Substitute.For<IManipulationRuleRepository>();
        rules.AddAsync(Arg.Do<ManipulationRule>(r => capturedId = r.Id), Arg.Any<CancellationToken>())
             .Returns(ci => ci.ArgAt<ManipulationRule>(0));

        var dto = new ImportRulesetDto(
            TrafficRules: [new ManipulationRule { Id = originalId, Name = "Imported Rule", Action = ManipulationRuleAction.Drop }]
        );

        var controller = MakeController(rules);
        var result = await controller.ImportRuleset(dto, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var json = JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"rulesImported\":1", json);
        Assert.NotEqual(originalId, capturedId);
    }

    [Fact]
    public async Task ImportRuleset_ImportsBreakpointsWithNewIds()
    {
        var originalId = Guid.NewGuid();
        var capturedId = Guid.Empty;

        var bps = Substitute.For<IBreakpointRepository>();
        bps.AddAsync(Arg.Do<Breakpoint>(b => capturedId = b.Id), Arg.Any<CancellationToken>())
           .Returns(ci => ci.ArgAt<Breakpoint>(0));

        var dto = new ImportRulesetDto(
            Breakpoints: [new Breakpoint { Id = originalId, Name = "BP", Language = ScriptLanguage.JavaScript, ScriptCode = "return true;" }]
        );

        var controller = MakeController(breakpoints: bps);
        var result = await controller.ImportRuleset(dto, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var json = JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"breakpointsImported\":1", json);
        Assert.NotEqual(originalId, capturedId);
    }

    [Fact]
    public async Task ImportRuleset_ImportsStandaloneContentRulesAsStandalone()
    {
        ContentReplacementRule? savedRule = null;

        var apiSpecs = Substitute.For<IApiSpecRepository>();
        apiSpecs.AddReplacementRuleAsync(
            Arg.Do<ContentReplacementRule>(r => savedRule = r),
            Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<ContentReplacementRule>(0));

        var dto = new ImportRulesetDto(
            ContentReplacementRules: [new ContentReplacementRule
            {
                Id = Guid.NewGuid(),
                ApiSpecDocumentId = Guid.NewGuid(),
                Host = "api.example.com",
                Name = "Block rule",
                MatchType = ContentMatchType.BodyRegex,
                MatchPattern = "secret"
            }]
        );

        var controller = MakeController(apiSpecs: apiSpecs);
        var result = await controller.ImportRuleset(dto, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        Assert.NotNull(savedRule);
        Assert.Null(savedRule!.ApiSpecDocumentId);
    }

    [Fact]
    public async Task ImportRuleset_EmptyBundle_ReturnsAllZeros()
    {
        var controller = MakeController();
        var result = await controller.ImportRuleset(new ImportRulesetDto(), CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var json = JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"rulesImported\":0", json);
        Assert.Contains("\"breakpointsImported\":0", json);
        Assert.Contains("\"contentRulesImported\":0", json);
        Assert.Contains("\"apiSpecsImported\":0", json);
    }

    [Fact]
    public async Task ImportRuleset_ApiSpecWithRules_CreatesBothWithLinkedIds()
    {
        var specId = Guid.NewGuid();
        var createdSpec = new ApiSpecDocument { Id = specId, Name = "Imported Spec", Host = "api.test" };
        Guid? ruleLinkedSpecId = null;

        var apiSpecs = Substitute.For<IApiSpecRepository>();
        apiSpecs.CreateAsync(Arg.Any<ApiSpecDocument>(), Arg.Any<CancellationToken>()).Returns(createdSpec);
        apiSpecs.AddReplacementRuleAsync(
            Arg.Do<ContentReplacementRule>(r => ruleLinkedSpecId = r.ApiSpecDocumentId),
            Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<ContentReplacementRule>(0));

        var dto = new ImportRulesetDto(
            ApiSpecs: [new ImportSpecBundleDto(
                Document: new ApiSpecDocument { Id = Guid.NewGuid(), Name = "Imported Spec", Host = "api.test" },
                Rules: [new ContentReplacementRule { Name = "Rule in spec", MatchType = ContentMatchType.BodyRegex, MatchPattern = "test" }]
            )]
        );

        var controller = MakeController(apiSpecs: apiSpecs);
        var result = await controller.ImportRuleset(dto, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var json = JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"apiSpecsImported\":1", json);
        Assert.Equal(specId, ruleLinkedSpecId);
    }
}
