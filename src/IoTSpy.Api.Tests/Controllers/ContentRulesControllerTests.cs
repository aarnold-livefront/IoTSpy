using System.Security.Claims;
using IoTSpy.Api;
using IoTSpy.Api.Controllers;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Manipulation.ApiSpec;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IoTSpy.Api.Tests.Controllers;

public class ContentRulesControllerTests
{
    private static ContentReplacementRule MakeRule(Guid? id = null, string host = "example.com") => new()
    {
        Id = id ?? Guid.NewGuid(),
        Host = host,
        Name = "Test Rule",
        MatchType = ContentMatchType.ContentType,
        MatchPattern = "image/*",
        Action = ContentReplacementAction.Redact,
        Enabled = true
    };

    private static ContentRulesController CreateController(
        IApiSpecRepository? repo = null,
        IAuditRepository? audit = null)
    {
        var specRepo = repo ?? Substitute.For<IApiSpecRepository>();
        var preview = new ReplacementPreviewService(
            new ContentReplacer(NullLogger<ContentReplacer>.Instance),
            specRepo,
            Substitute.For<ICaptureRepository>(),
            NullLogger<ReplacementPreviewService>.Instance);
        var controller = new ContentRulesController(specRepo, preview, audit ?? Substitute.For<IAuditRepository>());

        // Attach an authenticated user so CurrentUserId / CurrentUsername getters work.
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, "tester"),
            },
            authenticationType: "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    // ── List ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_WithoutHostFilter_ReturnsAllStandaloneRules()
    {
        var repo = Substitute.For<IApiSpecRepository>();
        repo.GetAllStandaloneRulesAsync(Arg.Any<CancellationToken>())
            .Returns([MakeRule(host: "a.com"), MakeRule(host: "b.com")]);

        var controller = CreateController(repo);
        var result = await controller.List(host: null, page: 1, pageSize: 100, ct: TestContext.Current.CancellationToken)
            as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"total\":2", json);
        await repo.Received(1).GetAllStandaloneRulesAsync(Arg.Any<CancellationToken>());
        await repo.DidNotReceive().GetStandaloneRulesForHostAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_WithHostFilter_QueriesByHost()
    {
        var repo = Substitute.For<IApiSpecRepository>();
        repo.GetStandaloneRulesForHostAsync("api.example.com", Arg.Any<CancellationToken>())
            .Returns([MakeRule(host: "api.example.com")]);

        var controller = CreateController(repo);
        var result = await controller.List(host: "api.example.com", page: 1, pageSize: 100,
            ct: TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        await repo.Received(1).GetStandaloneRulesForHostAsync("api.example.com", Arg.Any<CancellationToken>());
        await repo.DidNotReceive().GetAllStandaloneRulesAsync(Arg.Any<CancellationToken>());
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_HappyPath_PersistsRule()
    {
        var repo = Substitute.For<IApiSpecRepository>();
        repo.AddReplacementRuleAsync(Arg.Any<ContentReplacementRule>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ContentReplacementRule>());

        var controller = CreateController(repo);
        var dto = new CreateContentRuleDto(
            Host: "example.com",
            Name: "Block images",
            MatchType: ContentMatchType.ContentType,
            MatchPattern: "image/*",
            Action: ContentReplacementAction.Redact);

        var result = await controller.Create(dto, TestContext.Current.CancellationToken);

        Assert.IsType<CreatedResult>(result);
        await repo.Received(1).AddReplacementRuleAsync(Arg.Is<ContentReplacementRule>(r =>
            r.Host == "example.com" && r.Name == "Block images" && r.MatchPattern == "image/*"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_TrimsHost()
    {
        var repo = Substitute.For<IApiSpecRepository>();
        repo.AddReplacementRuleAsync(Arg.Any<ContentReplacementRule>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ContentReplacementRule>());

        var controller = CreateController(repo);
        var dto = new CreateContentRuleDto("  example.com  ", "n", ContentMatchType.ContentType, "image/*",
            ContentReplacementAction.Redact);

        await controller.Create(dto, TestContext.Current.CancellationToken);

        await repo.Received(1).AddReplacementRuleAsync(Arg.Is<ContentReplacementRule>(r => r.Host == "example.com"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("", "Name", "image/*")]
    [InlineData("  ", "Name", "image/*")]
    [InlineData("example.com", "", "image/*")]
    [InlineData("example.com", "Name", "")]
    public async Task Create_WithMissingRequiredField_ReturnsBadRequest(string host, string name, string pattern)
    {
        var repo = Substitute.For<IApiSpecRepository>();
        var controller = CreateController(repo);
        var dto = new CreateContentRuleDto(host, name, ContentMatchType.ContentType, pattern,
            ContentReplacementAction.Redact);

        var result = await controller.Create(dto, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
        await repo.DidNotReceive().AddReplacementRuleAsync(Arg.Any<ContentReplacementRule>(),
            Arg.Any<CancellationToken>());
    }

    // ── Path-traversal regression guards (PR #59) ─────────────────────────────

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\windows\\system32\\config\\sam")]
    [InlineData("/etc/shadow")]
    [InlineData("C:\\secrets.txt")]
    [InlineData("subdir/file.json")]
    public async Task Create_RejectsReplacementFilePathOutsideAssetsDirectory(string maliciousPath)
    {
        var repo = Substitute.For<IApiSpecRepository>();
        var controller = CreateController(repo);
        var dto = new CreateContentRuleDto(
            Host: "example.com",
            Name: "Bad",
            MatchType: ContentMatchType.ContentType,
            MatchPattern: "*/*",
            Action: ContentReplacementAction.ReplaceWithFile,
            ReplacementFilePath: maliciousPath);

        var result = await controller.Create(dto, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
        await repo.DidNotReceive().AddReplacementRuleAsync(Arg.Any<ContentReplacementRule>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_AcceptsBareFilenameAndPinsToAssetsDirectory()
    {
        var repo = Substitute.For<IApiSpecRepository>();
        repo.AddReplacementRuleAsync(Arg.Any<ContentReplacementRule>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ContentReplacementRule>());

        var controller = CreateController(repo);
        var dto = new CreateContentRuleDto("example.com", "ok", ContentMatchType.ContentType, "image/*",
            ContentReplacementAction.ReplaceWithFile, ReplacementFilePath: "asset.png");

        await controller.Create(dto, TestContext.Current.CancellationToken);

        var assetsRoot = Path.GetFullPath(AssetsPaths.AssetsDirectory);
        await repo.Received(1).AddReplacementRuleAsync(Arg.Is<ContentReplacementRule>(r =>
            r.ReplacementFilePath != null &&
            r.ReplacementFilePath.StartsWith(assetsRoot) &&
            r.ReplacementFilePath.EndsWith("asset.png")),
            Arg.Any<CancellationToken>());
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_WhenRuleMissing_ReturnsNotFound()
    {
        var repo = Substitute.For<IApiSpecRepository>();
        repo.GetAllStandaloneRulesAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ContentReplacementRule>());

        var controller = CreateController(repo);
        var result = await controller.Update(Guid.NewGuid(), new UpdateContentRuleDto(),
            TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Update_AppliesPatchAndAuditsChange()
    {
        var rule = MakeRule();
        var repo = Substitute.For<IApiSpecRepository>();
        repo.GetAllStandaloneRulesAsync(Arg.Any<CancellationToken>()).Returns([rule]);
        repo.UpdateReplacementRuleAsync(Arg.Any<ContentReplacementRule>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ContentReplacementRule>());
        var audit = Substitute.For<IAuditRepository>();

        var controller = CreateController(repo, audit);
        var dto = new UpdateContentRuleDto(Name: "Renamed", Enabled: false);
        var result = await controller.Update(rule.Id, dto, TestContext.Current.CancellationToken);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Renamed", rule.Name);
        Assert.False(rule.Enabled);
        await audit.Received(1).AddAsync(Arg.Is<AuditEntry>(a =>
            a.Action == "Update" && a.EntityType == "ContentReplacementRule" && a.EntityId == rule.Id.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_RejectsPathTraversalInReplacementFilePath()
    {
        var rule = MakeRule();
        var repo = Substitute.For<IApiSpecRepository>();
        repo.GetAllStandaloneRulesAsync(Arg.Any<CancellationToken>()).Returns([rule]);

        var controller = CreateController(repo);
        var dto = new UpdateContentRuleDto(ReplacementFilePath: "../../etc/passwd");
        var result = await controller.Update(rule.Id, dto, TestContext.Current.CancellationToken);

        Assert.IsType<BadRequestObjectResult>(result);
        await repo.DidNotReceive().UpdateReplacementRuleAsync(Arg.Any<ContentReplacementRule>(),
            Arg.Any<CancellationToken>());
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_WhenRuleExists_AuditsAndDeletes()
    {
        var rule = MakeRule();
        var repo = Substitute.For<IApiSpecRepository>();
        repo.GetRuleByIdAsync(rule.Id, Arg.Any<CancellationToken>()).Returns(rule);
        var audit = Substitute.For<IAuditRepository>();

        var controller = CreateController(repo, audit);
        var result = await controller.Delete(rule.Id, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        await repo.Received(1).DeleteReplacementRuleAsync(rule.Id, Arg.Any<CancellationToken>());
        await audit.Received(1).AddAsync(Arg.Is<AuditEntry>(a =>
            a.Action == "Delete" && a.EntityType == "ContentReplacementRule"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenRuleMissing_DoesNotAuditButStillReturnsNoContent()
    {
        var repo = Substitute.For<IApiSpecRepository>();
        repo.GetRuleByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ContentReplacementRule?)null);
        var audit = Substitute.For<IAuditRepository>();

        var controller = CreateController(repo, audit);
        var result = await controller.Delete(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        await audit.DidNotReceive().AddAsync(Arg.Any<AuditEntry>(), Arg.Any<CancellationToken>());
    }
}
