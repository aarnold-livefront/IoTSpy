using IoTSpy.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IoTSpy.Storage;

public class IoTSpyDbContext(DbContextOptions<IoTSpyDbContext> options) : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<CapturedRequest> Captures => Set<CapturedRequest>();
    public DbSet<CertificateEntry> Certificates => Set<CertificateEntry>();
    public DbSet<ProxySettings> ProxySettings => Set<ProxySettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => d.IpAddress).IsUnique();
            e.HasIndex(d => d.MacAddress);
        });

        modelBuilder.Entity<CapturedRequest>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.Timestamp);
            e.HasIndex(c => c.DeviceId);
            e.HasIndex(c => c.Host);
            e.HasOne(c => c.Device)
             .WithMany(d => d.Captures)
             .HasForeignKey(c => c.DeviceId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CertificateEntry>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => c.CommonName);
            e.HasIndex(c => c.IsRootCa);
        });

        modelBuilder.Entity<ProxySettings>(e =>
        {
            e.HasKey(p => p.Id);
        });

        // SQLite cannot ORDER BY DateTimeOffset columns; store all as Unix ms (long) so
        // LINQ ordering translates correctly.
        var dtoConverter = new ValueConverter<DateTimeOffset, long>(
            v => v.ToUnixTimeMilliseconds(),
            v => DateTimeOffset.FromUnixTimeMilliseconds(v));

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        foreach (var property in entityType.GetProperties())
        {
            if (property.ClrType == typeof(DateTimeOffset))
                property.SetValueConverter(dtoConverter);
        }
    }
}
