using Microsoft.EntityFrameworkCore;

namespace IoTSpy.Analytics.Tests;

internal static class TestDbContextFactory
{
    public static IoTSpy.Storage.IoTSpyDbContext Create()
    {
        var dbName = $"test-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<IoTSpy.Storage.IoTSpyDbContext>()
            .UseSqlite($"Data Source=file:{dbName}?mode=memory&cache=shared")
            .Options;

        var context = new IoTSpy.Storage.IoTSpyDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
