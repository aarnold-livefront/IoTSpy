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
    public DbSet<ManipulationRule> ManipulationRules => Set<ManipulationRule>();
    public DbSet<Breakpoint> Breakpoints => Set<Breakpoint>();
    public DbSet<ReplaySession> ReplaySessions => Set<ReplaySession>();
    public DbSet<FuzzerJob> FuzzerJobs => Set<FuzzerJob>();
    public DbSet<FuzzerResult> FuzzerResults => Set<FuzzerResult>();
public DbSet<OpenRtbEvent> OpenRtbEvents => Set<OpenRtbEvent>();
    public DbSet<PiiStrippingLog> PiiStrippingLogs => Set<PiiStrippingLog>();
    public DbSet<OpenRtbPiiPolicy> OpenRtbPiiPolicies => Set<OpenRtbPiiPolicy>();

    // Packet capture DbSet
    public DbSet<CaptureDevice> CaptureDevices => Set<CaptureDevice>();
    public DbSet<CapturedPacket> Packets => Set<CapturedPacket>();

    // Phase 9
    public DbSet<ScheduledScan> ScheduledScans => Set<ScheduledScan>();

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
            e.Property(p => p.CaptureRequestBodies).HasDefaultValue(true);
            e.Property(p => p.CaptureResponseBodies).HasDefaultValue(true);
        });

        modelBuilder.Entity<ManipulationRule>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.Enabled);
            e.HasIndex(r => r.Priority);
        });

        modelBuilder.Entity<Breakpoint>(e =>
        {
            e.HasKey(b => b.Id);
            e.HasIndex(b => b.Enabled);
        });

        modelBuilder.Entity<ReplaySession>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.OriginalCaptureId);
            e.HasIndex(r => r.CreatedAt);
            e.HasOne(r => r.OriginalCapture)
             .WithMany()
             .HasForeignKey(r => r.OriginalCaptureId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FuzzerJob>(e =>
        {
            e.HasKey(j => j.Id);
            e.HasIndex(j => j.BaseCaptureId);
            e.HasIndex(j => j.Status);
            e.HasOne(j => j.BaseCapture)
             .WithMany()
             .HasForeignKey(j => j.BaseCaptureId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FuzzerResult>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.FuzzerJobId);
            e.HasIndex(r => r.IsAnomaly);
            e.HasOne(r => r.FuzzerJob)
             .WithMany(j => j.Results)
             .HasForeignKey(r => r.FuzzerJobId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OpenRtbEvent>(e =>
        {
            e.HasKey(o => o.Id);
            e.HasIndex(o => o.CapturedRequestId);
            e.HasIndex(o => o.DetectedAt);
            e.HasIndex(o => o.Exchange);
            e.HasIndex(o => o.MessageType);
        });

        modelBuilder.Entity<PiiStrippingLog>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.CapturedRequestId);
            e.HasIndex(l => l.StrippedAt);
            e.HasIndex(l => l.Host);
            e.HasIndex(l => l.FieldPath);
        });

modelBuilder.Entity<OpenRtbPiiPolicy>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.Enabled);
            e.HasIndex(p => p.Priority);
        });

        modelBuilder.Entity<ScheduledScan>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.DeviceId);
            e.HasIndex(s => s.IsEnabled);
            e.HasOne(s => s.Device)
             .WithMany()
             .HasForeignKey(s => s.DeviceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CaptureDevice>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(100);
            e.Property(c => c.DisplayName).IsRequired().HasMaxLength(200);
            e.HasIndex(c => c.MacAddress);
            e.HasIndex(c => c.IpAddress);
        });

        modelBuilder.Entity<CapturedPacket>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Protocol).IsRequired().HasMaxLength(50);
            e.Property(p => p.Layer2Protocol).IsRequired().HasMaxLength(50);
            e.Property(p => p.Layer3Protocol).IsRequired().HasMaxLength(50);
            e.Property(p => p.Layer4Protocol).IsRequired().HasMaxLength(50);
            e.Property(p => p.SourceIp).IsRequired().HasMaxLength(45);
            e.Property(p => p.DestinationIp).IsRequired().HasMaxLength(45);
            e.Property(p => p.SourceMac).IsRequired().HasMaxLength(17);
            e.Property(p => p.DestinationMac).IsRequired().HasMaxLength(17);
            e.Property(p => p.PayloadPreview).HasMaxLength(2048);
            e.HasIndex(p => p.Timestamp);
            e.HasIndex(p => p.DeviceId);
            e.HasIndex(p => p.Protocol);
            e.HasIndex(p => p.SourceIp);
            e.HasIndex(p => p.DestinationIp);
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
