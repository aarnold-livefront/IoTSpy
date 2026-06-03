using IoTSpy.Core.Models;
using IoTSpy.Storage.Repositories;
using Xunit;

namespace IoTSpy.Storage.Tests.Repositories;

public class ScanScopeRepositoryTests : IDisposable
{
    private readonly IoTSpyDbContext _db = TestDbContextFactory.Create();
    public void Dispose() => _db.Dispose();

    private static ScanScope MakeScope(string cidr = "192.168.1.0/24", bool active = true) => new()
    {
        Name = "Test Scope",
        Cidr = cidr,
        IsActive = active,
        CreatedByUsername = "testuser"
    };

    [Fact]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        var repo = new ScanScopeRepository(_db);
        var result = await repo.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.Empty(result);
    }

    [Fact]
    public async Task AddAsync_PersistsScope()
    {
        var repo = new ScanScopeRepository(_db);
        var scope = MakeScope();

        var created = await repo.AddAsync(scope, TestContext.Current.CancellationToken);

        Assert.NotEqual(Guid.Empty, created.Id);
        var all = await repo.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.Single(all);
        Assert.Equal("192.168.1.0/24", all[0].Cidr);
    }

    [Fact]
    public async Task GetActiveAsync_ReturnsOnlyActiveScopes()
    {
        var repo = new ScanScopeRepository(_db);
        await repo.AddAsync(MakeScope("10.0.0.0/8",    active: true),  TestContext.Current.CancellationToken);
        await repo.AddAsync(MakeScope("172.16.0.0/12", active: false), TestContext.Current.CancellationToken);

        var active = await repo.GetActiveAsync(TestContext.Current.CancellationToken);

        Assert.Single(active);
        Assert.Equal("10.0.0.0/8", active[0].Cidr);
    }

    [Fact]
    public async Task GetByIdAsync_WhenFound_ReturnsScope()
    {
        var repo = new ScanScopeRepository(_db);
        var created = await repo.AddAsync(MakeScope(), TestContext.Current.CancellationToken);

        var result = await repo.GetByIdAsync(created.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissing_ReturnsNull()
    {
        var repo = new ScanScopeRepository(_db);
        var result = await repo.GetByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var repo = new ScanScopeRepository(_db);
        var created = await repo.AddAsync(MakeScope(), TestContext.Current.CancellationToken);

        created.IsActive = false;
        await repo.UpdateAsync(created, TestContext.Current.CancellationToken);

        var reloaded = await repo.GetByIdAsync(created.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(reloaded);
        Assert.False(reloaded.IsActive);
    }

    [Fact]
    public async Task DeleteAsync_RemovesScope()
    {
        var repo = new ScanScopeRepository(_db);
        var created = await repo.AddAsync(MakeScope(), TestContext.Current.CancellationToken);

        await repo.DeleteAsync(created.Id, TestContext.Current.CancellationToken);

        var all = await repo.GetAllAsync(TestContext.Current.CancellationToken);
        Assert.Empty(all);
    }

    [Fact]
    public async Task GetAllAsync_OrderedByCreatedAtDescending()
    {
        var repo = new ScanScopeRepository(_db);
        var first  = await repo.AddAsync(MakeScope("10.0.0.0/8"),      TestContext.Current.CancellationToken);
        await Task.Delay(5, TestContext.Current.CancellationToken); // ensure distinct timestamps
        var second = await repo.AddAsync(MakeScope("192.168.0.0/16"),  TestContext.Current.CancellationToken);

        var all = await repo.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(second.Id, all[0].Id);
        Assert.Equal(first.Id, all[1].Id);
    }
}
