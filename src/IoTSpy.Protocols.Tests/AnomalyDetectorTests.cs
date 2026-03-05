using Xunit;
using IoTSpy.Protocols.Anomaly;

namespace IoTSpy.Protocols.Tests;

public class AnomalyDetectorTests
{
    // ── Warm-up suppression ───────────────────────────────────────────────────

    [Fact]
    public void Record_BelowWarmUp_NoAlerts()
    {
        var detector = new AnomalyDetector { WarmUpSamples = 30, DeviationThreshold = 3.0 };

        // Feed 29 normal observations — no alerts expected
        for (var i = 0; i < 29; i++)
            Assert.Empty(detector.Record("host-a", 100, 1024, 200));
    }

    [Fact]
    public void Record_ExactlyAtWarmUp_AlertsCanFire()
    {
        // Low threshold so we're sure an anomaly fires once warm-up is reached
        var detector = new AnomalyDetector { WarmUpSamples = 5, DeviationThreshold = 0.5 };

        // 4 observations of 100 ms / 1024 bytes → establish baseline
        for (var i = 0; i < 4; i++)
            detector.Record("h", 100, 1024, 200);

        // 5th observation is an extreme outlier
        var alerts = detector.Record("h", 99999, 1024, 200);

        Assert.NotEmpty(alerts);
        Assert.Contains(alerts, a => a.AlertType == "ResponseTime");
    }

    // ── Response-time anomaly ─────────────────────────────────────────────────

    [Fact]
    public void Record_NormalDuration_NoAlert()
    {
        var detector = new AnomalyDetector { WarmUpSamples = 10, DeviationThreshold = 3.0 };

        // Establish baseline: 50 ms ± small noise
        for (var i = 0; i < 10; i++)
            detector.Record("h", 50 + (i % 3), 1024, 200);

        // 53 ms is well within 3 sigma — no alert
        var alerts = detector.Record("h", 53, 1024, 200);
        Assert.DoesNotContain(alerts, a => a.AlertType == "ResponseTime");
    }

    [Fact]
    public void Record_ExtremeDuration_AlertFired()
    {
        var detector = new AnomalyDetector { WarmUpSamples = 10, DeviationThreshold = 3.0 };

        for (var i = 0; i < 10; i++)
            detector.Record("h", 50, 1024, 200);

        // 10 000 ms is many sigma above the 50 ms baseline
        var alerts = detector.Record("h", 10000, 1024, 200);

        Assert.Contains(alerts, a => a.AlertType == "ResponseTime" && a.Host == "h");
    }

    [Fact]
    public void Record_DurationAlert_HasCorrectFields()
    {
        var detector = new AnomalyDetector { WarmUpSamples = 10, DeviationThreshold = 3.0 };

        for (var i = 0; i < 10; i++)
            detector.Record("target.example.com", 100, 2048, 200);

        var alerts = detector.Record("target.example.com", 50000, 2048, 200);

        var durationAlert = alerts.FirstOrDefault(a => a.AlertType == "ResponseTime");
        Assert.NotNull(durationAlert);
        Assert.Equal("target.example.com", durationAlert.Host);
        Assert.True(durationAlert.ActualValue > durationAlert.ExpectedValue);
        Assert.True(durationAlert.DeviationFactor >= 3.0);
        Assert.True(durationAlert.DetectedAt > DateTimeOffset.MinValue);
    }

    // ── Response-size anomaly ─────────────────────────────────────────────────

    [Fact]
    public void Record_ExtremeSize_AlertFired()
    {
        var detector = new AnomalyDetector { WarmUpSamples = 10, DeviationThreshold = 3.0 };

        for (var i = 0; i < 10; i++)
            detector.Record("h", 100, 1000, 200);

        var alerts = detector.Record("h", 100, 10_000_000, 200);

        Assert.Contains(alerts, a => a.AlertType == "ResponseSize");
    }

    [Fact]
    public void Record_NormalSize_NoSizeAlert()
    {
        var detector = new AnomalyDetector { WarmUpSamples = 10, DeviationThreshold = 3.0 };

        for (var i = 0; i < 10; i++)
            detector.Record("h", 100, 1000 + i, 200);

        var alerts = detector.Record("h", 100, 1005, 200);
        Assert.DoesNotContain(alerts, a => a.AlertType == "ResponseSize");
    }

    // ── Status-code anomaly ───────────────────────────────────────────────────

    [Fact]
    public void Record_UnexpectedStatusClass_AlertFired()
    {
        var detector = new AnomalyDetector { WarmUpSamples = 10, DeviationThreshold = 3.0 };

        // Baseline: all 200 OK
        for (var i = 0; i < 10; i++)
            detector.Record("h", 100, 1024, 200);

        // Suddenly a 500 — very unexpected
        var alerts = detector.Record("h", 100, 1024, 500);
        Assert.Contains(alerts, a => a.AlertType == "StatusCode");
    }

    [Fact]
    public void Record_SameStatusClass_NoStatusAlert()
    {
        var detector = new AnomalyDetector { WarmUpSamples = 10, DeviationThreshold = 3.0 };

        for (var i = 0; i < 10; i++)
            detector.Record("h", 100, 1024, 200);

        // 201 Created is in the same 2xx class
        var alerts = detector.Record("h", 100, 1024, 201);
        Assert.DoesNotContain(alerts, a => a.AlertType == "StatusCode");
    }

    [Fact]
    public void Record_FrequentStatusCode_NotFlaggedAsAnomaly()
    {
        var detector = new AnomalyDetector { WarmUpSamples = 10, DeviationThreshold = 3.0 };

        // Establish baseline with mixed 200 and 404 (404 becomes common)
        for (var i = 0; i < 5; i++) detector.Record("h", 100, 1024, 200);
        for (var i = 0; i < 5; i++) detector.Record("h", 100, 1024, 404);

        // 404 is now dominant or near-dominant — should not trigger status alert
        var alerts = detector.Record("h", 100, 1024, 404);
        // The code may or may not fire depending on ratio; key assertion is no duplicate firing
        // What we care about: if 404 has > 5% share, it should NOT be flagged
        Assert.DoesNotContain(alerts, a => a.AlertType == "StatusCode" && a.ActualValue == 404);
    }

    // ── Multiple alerts from single observation ───────────────────────────────

    [Fact]
    public void Record_MultipleAnomalies_AllAlertsReturned()
    {
        var detector = new AnomalyDetector { WarmUpSamples = 10, DeviationThreshold = 3.0 };

        for (var i = 0; i < 10; i++)
            detector.Record("h", 100, 1000, 200);

        // Extreme outlier on all dimensions
        var alerts = detector.Record("h", 100000, 100_000_000, 500);

        Assert.True(alerts.Count >= 2, $"Expected >=2 alerts, got {alerts.Count}");
    }

    // ── Baseline state ────────────────────────────────────────────────────────

    [Fact]
    public void GetBaselines_ReturnsAllHosts()
    {
        var detector = new AnomalyDetector { WarmUpSamples = 5 };

        detector.Record("host-a", 100, 1024, 200);
        detector.Record("host-b", 200, 2048, 200);
        detector.Record("host-a", 110, 1024, 200);

        var baselines = detector.GetBaselines();
        Assert.True(baselines.ContainsKey("host-a"));
        Assert.True(baselines.ContainsKey("host-b"));
        Assert.Equal(2, baselines["host-a"].SampleCount);
        Assert.Equal(1, baselines["host-b"].SampleCount);
    }

    [Fact]
    public void GetBaselines_MeanConverges()
    {
        var detector = new AnomalyDetector { WarmUpSamples = 5 };
        const int n = 100;
        const double expected = 250.0;

        for (var i = 0; i < n; i++)
            detector.Record("h", expected, 1024, 200);

        var baseline = detector.GetBaselines()["h"];
        Assert.Equal(n, baseline.SampleCount);
        Assert.Equal(expected, baseline.DurationMean, precision: 6);
    }

    [Fact]
    public void GetBaselines_StdDevIsZeroForConstantInput()
    {
        var detector = new AnomalyDetector { WarmUpSamples = 5 };

        for (var i = 0; i < 20; i++)
            detector.Record("h", 100, 1024, 200);

        var baseline = detector.GetBaselines()["h"];
        Assert.Equal(0, baseline.DurationStdDev, precision: 9);
        Assert.Equal(0, baseline.SizeStdDev, precision: 9);
    }

    // ── Reset ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_ClearsBaseline()
    {
        var detector = new AnomalyDetector { WarmUpSamples = 5 };

        for (var i = 0; i < 10; i++)
            detector.Record("h", 100, 1024, 200);

        Assert.True(detector.GetBaselines().ContainsKey("h"));

        detector.Reset("h");

        Assert.False(detector.GetBaselines().ContainsKey("h"));
    }

    [Fact]
    public void Reset_UnknownHost_DoesNotThrow()
    {
        var detector = new AnomalyDetector();
        var ex = Record.Exception(() => detector.Reset("non-existent-host"));
        Assert.Null(ex);
    }

    // ── Independent host baselines ────────────────────────────────────────────

    [Fact]
    public void Record_SeparateHosts_IndependentBaselines()
    {
        var detector = new AnomalyDetector { WarmUpSamples = 5, DeviationThreshold = 3.0 };

        // Host A: fast responses
        for (var i = 0; i < 10; i++) detector.Record("fast-host", 10, 512, 200);
        // Host B: slow responses
        for (var i = 0; i < 10; i++) detector.Record("slow-host", 5000, 10240, 200);

        var fastBaseline = detector.GetBaselines()["fast-host"];
        var slowBaseline = detector.GetBaselines()["slow-host"];

        Assert.Equal(10, fastBaseline.DurationMean, precision: 6);
        Assert.Equal(5000, slowBaseline.DurationMean, precision: 6);

        // Anomaly on fast-host should not affect slow-host
        var alerts = detector.Record("fast-host", 50000, 512, 200);
        Assert.Contains(alerts, a => a.Host == "fast-host" && a.AlertType == "ResponseTime");
    }
}
