using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;
using IoTSpy.Manipulation;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace IoTSpy.Manipulation.Tests;

public class ManipulationRuleCacheTests
{
    private static (ManipulationRuleCache cache, IManipulationRuleRepository repo) Make(
        List<ManipulationRule>? rules = null)
    {
        var memCache = new MemoryCache(new MemoryCacheOptions());
        var repo = Substitute.For<IManipulationRuleRepository>();
        repo.GetEnabledAsync(Arg.Any<CancellationToken>())
            .Returns(rules ?? [new ManipulationRule { Name = "R1", Action = ManipulationRuleAction.Drop }]);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(IManipulationRuleRepository)).Returns(repo);
        scope.ServiceProvider.Returns(provider);
        scopeFactory.CreateScope().Returns(scope);

        return (new ManipulationRuleCache(memCache, scopeFactory), repo);
    }

    [Fact]
    public async Task GetEnabledAsync_FirstCall_FetchesFromRepository()
    {
        var (cache, repo) = Make();

        var result = await cache.GetEnabledAsync(TestContext.Current.CancellationToken);

        Assert.Single(result);
        await repo.Received(1).GetEnabledAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEnabledAsync_SecondCall_ReturnsCachedValue()
    {
        var (cache, repo) = Make();

        await cache.GetEnabledAsync(TestContext.Current.CancellationToken);
        await cache.GetEnabledAsync(TestContext.Current.CancellationToken);

        // Repository should only be called once; second call hits the cache
        await repo.Received(1).GetEnabledAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalidate_ForcesRepositoryRefetchOnNextCall()
    {
        var (cache, repo) = Make();

        await cache.GetEnabledAsync(TestContext.Current.CancellationToken);
        cache.Invalidate();
        await cache.GetEnabledAsync(TestContext.Current.CancellationToken);

        await repo.Received(2).GetEnabledAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetEnabledAsync_EmptyRuleSet_CachesEmptyList()
    {
        var (cache, repo) = Make([]);

        var result = await cache.GetEnabledAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result);
        await repo.Received(1).GetEnabledAsync(Arg.Any<CancellationToken>());
    }
}
