using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IoTSpy.Storage;

/// <summary>
/// Used by dotnet-ef migrations tooling at design time.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<IoTSpyDbContext>
{
    public IoTSpyDbContext CreateDbContext(string[] args)
    {
        var opts = new DbContextOptionsBuilder<IoTSpyDbContext>()
            .UseSqlite("Data Source=iotspy_design.db")
            .Options;
        return new IoTSpyDbContext(opts);
    }
}
