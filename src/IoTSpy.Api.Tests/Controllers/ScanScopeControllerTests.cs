using IoTSpy.Api.Controllers;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace IoTSpy.Api.Tests.Controllers;

public class ScanScopeControllerTests
{
    private static ScanScope MakeScope(Guid? id = null, bool active = true) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "Lab Network",
        Cidr = "192.168.1.0/24",
        IsActive = active,
        CreatedByUsername = "admin"
    };

    private static ScanScopeController MakeController(IScanScopeRepository? repo = null)
    {
        var controller = new ScanScopeController(repo ?? Substitute.For<IScanScopeRepository>());
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
        };
        return controller;
    }

    [Fact]
    public async Task List_ReturnsAllScopesWithTotal()
    {
        var repo = Substitute.For<IScanScopeRepository>();
        repo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ScanScope> { MakeScope(), MakeScope() });

        var result = await MakeController(repo).List(TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"total\":2", json);
        Assert.Contains("\"items\"", json);
    }

    [Fact]
    public async Task Create_WithValidDto_ReturnsCreated()
    {
        var repo = Substitute.For<IScanScopeRepository>();
        repo.AddAsync(Arg.Any<ScanScope>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<ScanScope>());

        var dto = new CreateScanScopeDto("Lab", "10.0.0.0/8");
        var result = await MakeController(repo).Create(dto, TestContext.Current.CancellationToken);

        Assert.IsType<CreatedResult>(result);
        await repo.Received(1).AddAsync(Arg.Is<ScanScope>(s =>
            s.Name == "Lab" && s.Cidr == "10.0.0.0/8" && s.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithBlankName_ReturnsBadRequest()
    {
        var result = await MakeController().Create(new CreateScanScopeDto("", "10.0.0.0/8"), TestContext.Current.CancellationToken);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Theory]
    [InlineData("not-a-cidr")]
    [InlineData("192.168.1.0/99")]
    [InlineData("256.0.0.1/24")]
    public async Task Create_WithInvalidCidr_ReturnsBadRequest(string cidr)
    {
        var result = await MakeController().Create(new CreateScanScopeDto("Test", cidr), TestContext.Current.CancellationToken);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Toggle_WhenFound_FlipsIsActiveAndReturnsOk()
    {
        var scope = MakeScope(active: true);
        var repo = Substitute.For<IScanScopeRepository>();
        repo.GetByIdAsync(scope.Id, Arg.Any<CancellationToken>()).Returns(scope);

        var result = await MakeController(repo).Toggle(scope.Id, TestContext.Current.CancellationToken) as OkObjectResult;

        Assert.NotNull(result);
        await repo.Received(1).UpdateAsync(Arg.Is<ScanScope>(s => !s.IsActive), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Toggle_WhenMissing_ReturnsNotFound()
    {
        var repo = Substitute.For<IScanScopeRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ScanScope?)null);

        var result = await MakeController(repo).Toggle(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_WhenFound_ReturnsNoContent()
    {
        var scope = MakeScope();
        var repo = Substitute.For<IScanScopeRepository>();
        repo.GetByIdAsync(scope.Id, Arg.Any<CancellationToken>()).Returns(scope);

        var result = await MakeController(repo).Delete(scope.Id, TestContext.Current.CancellationToken);

        Assert.IsType<NoContentResult>(result);
        await repo.Received(1).DeleteAsync(scope.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenMissing_ReturnsNotFoundAndDoesNotDelete()
    {
        var repo = Substitute.For<IScanScopeRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ScanScope?)null);

        var result = await MakeController(repo).Delete(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.IsType<NotFoundResult>(result);
        await repo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Create_RequiresAdminRole()
    {
        var method = typeof(ScanScopeController).GetMethod(nameof(ScanScopeController.Create))!;
        var attr = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();
        Assert.NotNull(attr);
        Assert.Equal("admin", attr.Roles);
    }

    [Fact]
    public void Delete_RequiresAdminRole()
    {
        var method = typeof(ScanScopeController).GetMethod(nameof(ScanScopeController.Delete))!;
        var attr = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();
        Assert.NotNull(attr);
        Assert.Equal("admin", attr.Roles);
    }
}
