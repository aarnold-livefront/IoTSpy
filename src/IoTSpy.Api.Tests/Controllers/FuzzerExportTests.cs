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

public class FuzzerExportTests
{
    private static ManipulationController MakeController(
        IFuzzerJobRepository fuzzerJobs,
        IApiSpecRepository? apiSpecs = null)
    {
        return new ManipulationController(
            Substitute.For<IManipulationService>(),
            Substitute.For<IManipulationRuleRepository>(),
            Substitute.For<IBreakpointRepository>(),
            Substitute.For<IReplaySessionRepository>(),
            fuzzerJobs,
            Substitute.For<ICaptureRepository>(),
            apiSpecs ?? Substitute.For<IApiSpecRepository>());
    }

    [Fact]
    public async Task ExportFuzzerResults_WhenJobNotFound_ReturnsNotFound()
    {
        var fuzzerJobs = Substitute.For<IFuzzerJobRepository>();
        fuzzerJobs.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((FuzzerJob?)null);

        var controller = MakeController(fuzzerJobs);
        var result = await controller.ExportFuzzerResults(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ExportFuzzerResults_ReturnsJsonWithCorrectContentDisposition()
    {
        var id = Guid.NewGuid();
        var job = new FuzzerJob { Id = id, Strategy = FuzzerStrategy.Random, MutationCount = 10 };
        var results = new List<FuzzerResult>
        {
            new() { FuzzerJobId = id, MutationIndex = 0, ResponseStatusCode = 200, IsAnomaly = false },
            new() { FuzzerJobId = id, MutationIndex = 1, ResponseStatusCode = 500, IsAnomaly = true, AnomalyReason = "Server error" }
        };

        var fuzzerJobs = Substitute.For<IFuzzerJobRepository>();
        fuzzerJobs.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(job);
        fuzzerJobs.GetResultsAsync(id, Arg.Any<CancellationToken>()).Returns(results);

        var controller = MakeController(fuzzerJobs);
        var result = await controller.ExportFuzzerResults(id, CancellationToken.None) as FileContentResult;

        Assert.NotNull(result);
        Assert.Equal("application/json", result.ContentType);
        Assert.Equal($"fuzzer-{id}.json", result.FileDownloadName);

        var json = Encoding.UTF8.GetString(result.FileContents);
        var doc = JsonDocument.Parse(json);
        Assert.Equal(id.ToString(), doc.RootElement.GetProperty("fuzzerId").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("results").GetArrayLength());
    }

    [Fact]
    public async Task ExportFuzzerResults_WithEmptyResults_ReturnsEmptyArray()
    {
        var id = Guid.NewGuid();
        var job = new FuzzerJob { Id = id };

        var fuzzerJobs = Substitute.For<IFuzzerJobRepository>();
        fuzzerJobs.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(job);
        fuzzerJobs.GetResultsAsync(id, Arg.Any<CancellationToken>()).Returns(new List<FuzzerResult>());

        var controller = MakeController(fuzzerJobs);
        var result = await controller.ExportFuzzerResults(id, CancellationToken.None) as FileContentResult;

        Assert.NotNull(result);
        var json = Encoding.UTF8.GetString(result.FileContents);
        var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("results").GetArrayLength());
    }
}
