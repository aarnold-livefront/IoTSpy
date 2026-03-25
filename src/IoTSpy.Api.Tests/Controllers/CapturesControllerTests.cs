using IoTSpy.Api.Controllers;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace IoTSpy.Api.Tests.Controllers;

public class CapturesControllerTests
{
    private static CapturedRequest MakeCapture(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Host = "example.com",
        Method = "GET",
        Path = "/api/test",
        StatusCode = 200,
        Timestamp = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task List_ReturnsPagedResults()
    {
        var repo = Substitute.For<ICaptureRepository>();
        var captures = new List<CapturedRequest> { MakeCapture(), MakeCapture() };
        repo.GetPagedAsync(Arg.Any<CaptureFilter>(), 1, 50, Arg.Any<CancellationToken>()).Returns(captures);
        repo.CountAsync(Arg.Any<CaptureFilter>(), Arg.Any<CancellationToken>()).Returns(2);

        var controller = new CapturesController(repo);
        var result = await controller.List(null, null, null, null, null, null, null, null) as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("\"total\":2", json);
    }

    [Fact]
    public async Task Get_WhenFound_ReturnsCapture()
    {
        var id = Guid.NewGuid();
        var repo = Substitute.For<ICaptureRepository>();
        repo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(MakeCapture(id));

        var controller = new CapturesController(repo);
        var result = await controller.Get(id) as OkObjectResult;

        Assert.NotNull(result);
        Assert.IsType<CapturedRequest>(result.Value);
    }

    [Fact]
    public async Task Get_WhenNotFound_ReturnsNotFound()
    {
        var repo = Substitute.For<ICaptureRepository>();
        repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((CapturedRequest?)null);

        var controller = new CapturesController(repo);
        var result = await controller.Get(Guid.NewGuid());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_CallsRepoDeleteAndReturnsNoContent()
    {
        var id = Guid.NewGuid();
        var repo = Substitute.For<ICaptureRepository>();
        repo.DeleteAsync(id, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var controller = new CapturesController(repo);
        var result = await controller.Delete(id);

        Assert.IsType<NoContentResult>(result);
        await repo.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Clear_CallsRepoClearAndReturnsNoContent()
    {
        var repo = Substitute.For<ICaptureRepository>();
        repo.ClearAsync(Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var controller = new CapturesController(repo);
        var result = await controller.Clear(null);

        Assert.IsType<NoContentResult>(result);
        await repo.Received(1).ClearAsync(null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_PageSizeClampedTo200()
    {
        var repo = Substitute.For<ICaptureRepository>();
        repo.GetPagedAsync(Arg.Any<CaptureFilter>(), 1, 200, Arg.Any<CancellationToken>()).Returns(new List<CapturedRequest>());
        repo.CountAsync(Arg.Any<CaptureFilter>(), Arg.Any<CancellationToken>()).Returns(0);

        var controller = new CapturesController(repo);
        await controller.List(null, null, null, null, null, null, null, null, pageSize: 9999);

        await repo.Received(1).GetPagedAsync(Arg.Any<CaptureFilter>(), 1, 200, Arg.Any<CancellationToken>());
    }
}
