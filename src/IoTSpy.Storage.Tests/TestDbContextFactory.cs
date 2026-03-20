using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Storage.Tests;

/// <summary>
/// Creates an in-process SQLite IoTSpyDbContext for repository tests.
/// Each test gets its own isolated database file via a unique GUID.
/// </summary>
public static class TestDbContextFactory
{
    public static IoTSpyDbContext Create()
    {
        // Use a unique in-memory SQLite database per call
        var dbName = $"test-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<IoTSpyDbContext>()
            .UseSqlite($"Data Source=file:{dbName}?mode=memory&cache=shared")
            .Options;

        var context = new IoTSpyDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
