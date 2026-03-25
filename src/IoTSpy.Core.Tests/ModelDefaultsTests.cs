using IoTSpy.Core.Enums;
using IoTSpy.Core.Models;
using Xunit;

namespace IoTSpy.Core.Tests;

public class ModelDefaultsTests
{
    // ── Device ──────────────────────────────────────────────────────

    [Fact]
    public void Device_NewInstance_HasNonEmptyGuidId()
    {
        var device = new Device();
        Assert.NotEqual(Guid.Empty, device.Id);
    }

    [Fact]
    public void Device_NewInstance_HasDefaultSecurityScoreMinusOne()
    {
        var device = new Device();
        Assert.Equal(-1, device.SecurityScore);
    }

    [Fact]
    public void Device_NewInstance_InterceptionEnabledByDefault()
    {
        var device = new Device();
        Assert.True(device.InterceptionEnabled);
    }

    [Fact]
    public void Device_NewInstance_HasEmptyStringProperties()
    {
        var device = new Device();
        Assert.Equal(string.Empty, device.IpAddress);
        Assert.Equal(string.Empty, device.MacAddress);
        Assert.Equal(string.Empty, device.Hostname);
        Assert.Equal(string.Empty, device.Vendor);
        Assert.Equal(string.Empty, device.Label);
        Assert.Equal(string.Empty, device.Notes);
    }

    [Fact]
    public void Device_NewInstance_HasEmptyCapturesList()
    {
        var device = new Device();
        Assert.NotNull(device.Captures);
        Assert.Empty(device.Captures);
    }

    // ── CapturedRequest ─────────────────────────────────────────────

    [Fact]
    public void CapturedRequest_NewInstance_HasNonEmptyId()
    {
        var capture = new CapturedRequest();
        Assert.NotEqual(Guid.Empty, capture.Id);
    }

    [Fact]
    public void CapturedRequest_NewInstance_HasDefaultProtocolHttp()
    {
        var capture = new CapturedRequest();
        Assert.Equal(InterceptionProtocol.Http, capture.Protocol);
    }

    [Fact]
    public void CapturedRequest_NewInstance_HasTlsMetadataJsonEmpty()
    {
        var capture = new CapturedRequest();
        Assert.Equal(string.Empty, capture.TlsMetadataJson);
    }

    // ── TlsMetadata ────────────────────────────────────────────────

    [Fact]
    public void TlsMetadata_NewInstance_HasEmptyDefaults()
    {
        var meta = new TlsMetadata();
        Assert.Equal(string.Empty, meta.SniHostname);
        Assert.Equal(string.Empty, meta.Ja3Hash);
        Assert.Equal(string.Empty, meta.Ja3Raw);
        Assert.Equal(string.Empty, meta.Ja3sHash);
        Assert.Equal(string.Empty, meta.Ja3sRaw);
        Assert.Equal(string.Empty, meta.CertSubject);
        Assert.Equal(string.Empty, meta.CertIssuer);
        Assert.Equal(string.Empty, meta.CertSerial);
        Assert.Equal(string.Empty, meta.CertSha256Fingerprint);
    }

    [Fact]
    public void TlsMetadata_NewInstance_HasEmptyLists()
    {
        var meta = new TlsMetadata();
        Assert.NotNull(meta.ClientCipherSuites);
        Assert.Empty(meta.ClientCipherSuites);
        Assert.NotNull(meta.ClientExtensions);
        Assert.Empty(meta.ClientExtensions);
        Assert.NotNull(meta.ServerExtensions);
        Assert.Empty(meta.ServerExtensions);
        Assert.NotNull(meta.CertSanList);
        Assert.Empty(meta.CertSanList);
    }

    [Fact]
    public void TlsMetadata_NewInstance_HasZeroByteCounters()
    {
        var meta = new TlsMetadata();
        Assert.Equal(0, meta.ClientToServerBytes);
        Assert.Equal(0, meta.ServerToClientBytes);
    }

    // ── ProxySettings ──────────────────────────────────────────────

    [Fact]
    public void ProxySettings_NewInstance_HasDefaultPort8888()
    {
        var settings = new ProxySettings();
        Assert.Equal(8888, settings.ProxyPort);
    }

    [Fact]
    public void ProxySettings_NewInstance_CaptureTlsEnabledByDefault()
    {
        var settings = new ProxySettings();
        Assert.True(settings.CaptureTls);
    }

    [Fact]
    public void ProxySettings_NewInstance_SslStripDisabledByDefault()
    {
        var settings = new ProxySettings();
        Assert.False(settings.SslStrip);
    }

    [Fact]
    public void ProxySettings_NewInstance_CaptureRequestBodiesEnabled()
    {
        var settings = new ProxySettings();
        Assert.True(settings.CaptureRequestBodies);
        Assert.True(settings.CaptureResponseBodies);
    }

    [Fact]
    public void ProxySettings_NewInstance_DefaultMaxBodySize1024()
    {
        var settings = new ProxySettings();
        Assert.Equal(1024, settings.MaxBodySizeKb);
    }

    [Fact]
    public void ProxySettings_NewInstance_DefaultModeExplicitProxy()
    {
        var settings = new ProxySettings();
        Assert.Equal(ProxyMode.ExplicitProxy, settings.Mode);
    }

    // ── User ───────────────────────────────────────────────────────

    [Fact]
    public void User_NewInstance_HasNonEmptyId()
    {
        var user = new User();
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Fact]
    public void User_NewInstance_DefaultRoleIsViewer()
    {
        var user = new User();
        Assert.Equal(UserRole.Viewer, user.Role);
    }

    [Fact]
    public void User_NewInstance_IsEnabledByDefault()
    {
        var user = new User();
        Assert.True(user.IsEnabled);
    }

    [Fact]
    public void User_NewInstance_HasNoLastLogin()
    {
        var user = new User();
        Assert.Null(user.LastLoginAt);
    }

    // ── AuditEntry ─────────────────────────────────────────────────

    [Fact]
    public void AuditEntry_NewInstance_HasNonEmptyId()
    {
        var entry = new AuditEntry();
        Assert.NotEqual(Guid.Empty, entry.Id);
    }

    [Fact]
    public void AuditEntry_NewInstance_HasEmptyDefaults()
    {
        var entry = new AuditEntry();
        Assert.Equal(string.Empty, entry.Username);
        Assert.Equal(string.Empty, entry.Action);
        Assert.Equal(string.Empty, entry.EntityType);
        Assert.Null(entry.EntityId);
        Assert.Null(entry.Details);
    }

    // ── ScanJob ────────────────────────────────────────────────────

    [Fact]
    public void ScanJob_NewInstance_DefaultStatusIsPending()
    {
        var job = new ScanJob();
        Assert.Equal(ScanStatus.Pending, job.Status);
    }

    [Fact]
    public void ScanJob_NewInstance_DefaultPortRange()
    {
        var job = new ScanJob();
        Assert.Equal("1-1024", job.PortRange);
    }

    [Fact]
    public void ScanJob_NewInstance_AllScanFeaturesEnabled()
    {
        var job = new ScanJob();
        Assert.True(job.EnableFingerprinting);
        Assert.True(job.EnableCredentialTest);
        Assert.True(job.EnableCveLookup);
        Assert.True(job.EnableConfigAudit);
    }

    // ── ManipulationRule ───────────────────────────────────────────

    [Fact]
    public void ManipulationRule_NewInstance_EnabledByDefault()
    {
        var rule = new ManipulationRule();
        Assert.True(rule.Enabled);
    }

    [Fact]
    public void ManipulationRule_NewInstance_DefaultPhaseIsRequest()
    {
        var rule = new ManipulationRule();
        Assert.Equal(ManipulationPhase.Request, rule.Phase);
    }

    // ── DashboardLayout ────────────────────────────────────────────

    [Fact]
    public void DashboardLayout_NewInstance_HasDefaultName()
    {
        var layout = new DashboardLayout();
        Assert.Equal("Default", layout.Name);
    }

    [Fact]
    public void DashboardLayout_NewInstance_HasEmptyJsonDefaults()
    {
        var layout = new DashboardLayout();
        Assert.Equal("{}", layout.LayoutJson);
        Assert.Equal("{}", layout.FiltersJson);
    }

    // ── Enum coverage ──────────────────────────────────────────────

    [Fact]
    public void UserRole_HasThreeValues()
    {
        var values = Enum.GetValues<UserRole>();
        Assert.Equal(3, values.Length);
        Assert.Contains(UserRole.Viewer, values);
        Assert.Contains(UserRole.Operator, values);
        Assert.Contains(UserRole.Admin, values);
    }

    [Fact]
    public void InterceptionProtocol_IncludesTlsPassthrough()
    {
        var values = Enum.GetValues<InterceptionProtocol>();
        Assert.Contains(InterceptionProtocol.TlsPassthrough, values);
        Assert.Contains(InterceptionProtocol.WebSocket, values);
        Assert.Contains(InterceptionProtocol.Grpc, values);
        Assert.Contains(InterceptionProtocol.Modbus, values);
    }
}
