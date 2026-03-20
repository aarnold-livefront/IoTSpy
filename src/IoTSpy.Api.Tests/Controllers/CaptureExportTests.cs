using IoTSpy.Api.Controllers;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using System.Text;
using System.Text.Json;
using Xunit;

namespace IoTSpy.Api.Tests.Controllers;

public class CaptureExportTests
{
    private static CapturedRequest MakeCapture() => new()
    {
        Id = Guid.NewGuid(),
        Scheme = "https",
        Host = "example.com",
        Path = "/api/test",
        Method = "GET",
        StatusCode = 200,
        RequestBodySize = 10,
        ResponseBodySize = 512,
        IsModified = false,
        Protocol = IoTSpy.Core.Enums.InterceptionProtocol.Http,
        DurationMs = 100,
        Timestamp = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task ExportCsv_ReturnsCsvContentType()
    {
        var repo = Substitute.For<ICaptureRepository>();
        repo.GetPagedAsync(Arg.Any<CaptureFilter>(), 1, 10_000, Arg.Any<CancellationToken>())
            .Returns(new List<CapturedRequest> { MakeCapture() });

        var controller = new CapturesController(repo);
        var result = await controller.ExportCsv(null, null, null, default) as FileContentResult;

        Assert.NotNull(result);
        Assert.Equal("text/csv", result.ContentType);
        var csv = Encoding.UTF8.GetString(result.FileContents);
        Assert.Contains("Id,Timestamp,Method", csv);
        Assert.Contains("GET", csv);
    }

    [Fact]
    public async Task ExportJson_ReturnsJsonArray()
    {
        var capture = MakeCapture();
        var repo = Substitute.For<ICaptureRepository>();
        repo.GetPagedAsync(Arg.Any<CaptureFilter>(), 1, 10_000, Arg.Any<CancellationToken>())
            .Returns(new List<CapturedRequest> { capture });

        var controller = new CapturesController(repo);
        var result = await controller.ExportJson(null, null, null, default) as FileContentResult;

        Assert.NotNull(result);
        Assert.Equal("application/json", result.ContentType);
        var json = Encoding.UTF8.GetString(result.FileContents);
        var parsed = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Array, parsed.RootElement.ValueKind);
        Assert.Equal(1, parsed.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task ExportHar_HasCorrectVersion()
    {
        var repo = Substitute.For<ICaptureRepository>();
        repo.GetPagedAsync(Arg.Any<CaptureFilter>(), 1, 10_000, Arg.Any<CancellationToken>())
            .Returns(new List<CapturedRequest> { MakeCapture() });

        var controller = new CapturesController(repo);
        var result = await controller.ExportHar(null, null, null, default) as FileContentResult;

        Assert.NotNull(result);
        var json = Encoding.UTF8.GetString(result.FileContents);
        Assert.Contains("\"version\":\"1.2\"", json.Replace(" ", ""));
        Assert.Contains("entries", json);
    }
}
