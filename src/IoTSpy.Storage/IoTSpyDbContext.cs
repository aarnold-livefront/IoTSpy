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
    public DbSet<ScanJob> ScanJobs => Set<ScanJob>();
    public DbSet<ScanFinding> ScanFindings => Set<ScanFinding>();

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

        modelBuilder.Entity<ScanJob>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.DeviceId);
            e.HasIndex(s => s.Status);
            e.HasIndex(s => s.CreatedAt);
            e.HasOne(s => s.Device)
             .WithMany()
             .HasForeignKey(s => s.DeviceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScanFinding>(e =>
        {
            e.HasKey(f => f.Id);
            e.HasIndex(f => f.ScanJobId);
            e.HasIndex(f => f.Type);
            e.HasIndex(f => f.Severity);
            e.HasOne(f => f.ScanJob)
             .WithMany(s => s.Findings)
             .HasForeignKey(f => f.ScanJobId)
             .OnDelete(DeleteBehavior.Cascade);
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

        var nullableDtoConverter = new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : null,
            v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        foreach (var property in entityType.GetProperties())
        {
            if (property.ClrType == typeof(DateTimeOffset))
                property.SetValueConverter(dtoConverter);
            else if (property.ClrType == typeof(DateTimeOffset?))
                property.SetValueConverter(nullableDtoConverter);
        }
    }
}
