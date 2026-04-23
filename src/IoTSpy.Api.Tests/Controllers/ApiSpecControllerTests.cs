using IoTSpy.Api.Controllers;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Manipulation.ApiSpec;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace IoTSpy.Api.Tests.Controllers;

public class ApiSpecControllerTests
{
    private static ApiSpecDocument MakeSpec(Guid? id = null, string host = "api.example.com") => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "Test Spec",
        Host = host,
        OpenApiJson = """{"openapi":"3.0.3","info":{"title":"Test","version":"1.0.0"},"paths":{}}""",
        Status = ApiSpecStatus.Draft
    };

    private static ContentReplacementRule MakeRule(Guid specId) => new()
    {
        ApiSpecDocumentId = specId,
        Name = "Test Rule",
        MatchType = ContentMatchType.ContentType,
        MatchPattern = "image/*",
        Action = ContentReplacementAction.Redact
    };

    private static ApiSpecController CreateController(
        IApiSpecService? service = null,
        IApiSpecRepository? repo = null,
        ReplacementPreviewService? preview = null)
    {
        var specRepo = repo ?? Substitute.For<IApiSpecRepository>();
        return new ApiSpecController(
            service ?? Substitute.For<IApiSpecService>(),
            specRepo,
            preview ?? new ReplacementPreviewService(
                new ContentReplacer(Microsoft.Extensions.Logging.Abstractions.NullLogger<ContentReplacer>.Instance),
                specRepo,
                Substitute.For<ICaptureRepository>(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<ReplacementPreviewService>.Instance));
    }

    // ── List ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsAllSpecs()
    {
        var repo = Substitute.For<IApiSpecRepository>();
        repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns([MakeSpec(), MakeSpec()]);

        var controller = CreateController(repo: repo);
        var result = await controller.List() as OkObjectResult;

        Assert.NotNull(result);
        var specs = Assert.IsType<List<ApiSpecDocument>>(result.Value);
        Assert.Equal(2, specs.Count);
    }

    // ── Get ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_WhenFound_ReturnsSpec()
    {
        var id = Guid.NewGuid();
        var repo = Substitute.For<IApiSpecRepository>();
        repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(MakeSpec(id));

        var controller = CreateController(repo: repo);
        var result = await controller.Get(id) as OkObjectResult;

        Assert.NotNull(result);
        Assert.IsType<ApiSpecDocument>(result.Value);
    }

    [Fact]
    public async Task Get_WhenNotFound_ReturnsNotFound()
    {
        var repo = Substitute.For<IApiSpecRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ApiSpecDocument?)null);

        var controller = CreateController(repo: repo);
        var result = await controller.Get(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    // ── Generate ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_WithHost_ReturnsCreated()
    {
        var service = Substitute.For<IApiSpecService>();
        var expectedDoc = MakeSpec();
        service.GenerateFromTrafficAsync(Arg.Any<ApiSpecGenerationRequest>(), Arg.Any<CancellationToken>())
            .Returns(expectedDoc);

        var controller = CreateController(service: service);
        var result = await controller.Generate(
            new ApiSpecGenerationRequest { Host = "api.example.com" },
            CancellationToken.None) as CreatedResult;

        Assert.NotNull(result);
        Assert.IsType<ApiSpecDocument>(result.Value);
    }

    [Fact]
    public async Task Generate_WithoutHost_ReturnsBadRequest()
    {
        var controller = CreateController();
        var result = await controller.Generate(
            new ApiSpecGenerationRequest { Host = "" },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Import_ValidJson_ReturnsCreated()
    {
        var service = Substitute.For<IApiSpecService>();
        service.ImportAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(MakeSpec());

        var controller = CreateController(service: service);
        var result = await controller.Import(
            new ApiSpecController.ImportSpecDto("""{"openapi":"3.0.3"}""", "Test"),
            CancellationToken.None) as CreatedResult;

        Assert.NotNull(result);
    }

    [Fact]
    public async Task Import_EmptyJson_ReturnsBadRequest()
    {
        var controller = CreateController();
        var result = await controller.Import(
            new ApiSpecController.ImportSpecDto(""),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    // ── Export ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Export_WhenFound_ReturnsFile()
    {
        var service = Substitute.For<IApiSpecService>();
        service.ExportAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns("""{"openapi":"3.0.3"}""");

        var controller = CreateController(service: service);
        var result = await controller.Export(Guid.NewGuid(), CancellationToken.None) as FileContentResult;

        Assert.NotNull(result);
        Assert.Equal("application/json", result.ContentType);
    }

    [Fact]
    public async Task Export_WhenNotFound_ReturnsNotFound()
    {
        var service = Substitute.For<IApiSpecService>();
        service.ExportAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<string>(x => throw new KeyNotFoundException());

        var controller = CreateController(service: service);
        var result = await controller.Export(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_WhenFound_ReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var repo = Substitute.For<IApiSpecRepository>();
        repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(MakeSpec(id));

        var controller = CreateController(repo: repo);
        var result = await controller.Delete(id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        await repo.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenNotFound_ReturnsNotFound()
    {
        var repo = Substitute.For<IApiSpecRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ApiSpecDocument?)null);

        var controller = CreateController(repo: repo);
        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // ── Activate / Deactivate ─────────────────────────────────────────────────

    [Fact]
    public async Task Activate_SetsActiveAndMockEnabled()
    {
        var id = Guid.NewGuid();
        var repo = Substitute.For<IApiSpecRepository>();
        var spec = MakeSpec(id);
        repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(spec);

        var controller = CreateController(repo: repo);
        var result = await controller.Activate(id, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var updated = Assert.IsType<ApiSpecDocument>(result.Value);
        Assert.Equal(ApiSpecStatus.Active, updated.Status);
        Assert.True(updated.MockEnabled);
    }

    [Fact]
    public async Task Deactivate_ClearsMockEnabled()
    {
        var id = Guid.NewGuid();
        var repo = Substitute.For<IApiSpecRepository>();
        var spec = MakeSpec(id);
        spec.MockEnabled = true;
        repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(spec);

        var controller = CreateController(repo: repo);
        var result = await controller.Deactivate(id, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var updated = Assert.IsType<ApiSpecDocument>(result.Value);
        Assert.False(updated.MockEnabled);
    }

    // ── Replacement Rules ─────────────────────────────────────────────────────

    [Fact]
    public async Task ListRules_ReturnsRules()
    {
        var specId = Guid.NewGuid();
        var repo = Substitute.For<IApiSpecRepository>();
        repo.GetReplacementRulesAsync(specId, Arg.Any<CancellationToken>())
            .Returns([MakeRule(specId), MakeRule(specId)]);

        var controller = CreateController(repo: repo);
        var result = await controller.ListRules(specId, CancellationToken.None) as OkObjectResult;

        Assert.NotNull(result);
        var rules = Assert.IsType<List<ContentReplacementRule>>(result.Value);
        Assert.Equal(2, rules.Count);
    }

    [Fact]
    public async Task CreateRule_WhenSpecExists_ReturnsCreated()
    {
        var specId = Guid.NewGuid();
        var repo = Substitute.For<IApiSpecRepository>();
        repo.GetByIdAsync(specId, Arg.Any<CancellationToken>()).Returns(MakeSpec(specId));
        repo.AddReplacementRuleAsync(Arg.Any<ContentReplacementRule>(), Arg.Any<CancellationToken>())
            .Returns(x => (ContentReplacementRule)x[0]);

        var controller = CreateController(repo: repo);
        var dto = new ApiSpecController.CreateReplacementRuleDto(
            "Test Rule",
            ContentMatchType.ContentType,
            "image/*",
            ContentReplacementAction.Redact);

        var result = await controller.CreateRule(specId, dto, CancellationToken.None) as CreatedResult;

        Assert.NotNull(result);
        Assert.IsType<ContentReplacementRule>(result.Value);
    }

    [Fact]
    public async Task CreateRule_WhenSpecNotFound_ReturnsNotFound()
    {
        var repo = Substitute.For<IApiSpecRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ApiSpecDocument?)null);

        var controller = CreateController(repo: repo);
        var dto = new ApiSpecController.CreateReplacementRuleDto(
            "Test Rule",
            ContentMatchType.ContentType,
            "image/*",
            ContentReplacementAction.Redact);

        var result = await controller.CreateRule(Guid.NewGuid(), dto, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
