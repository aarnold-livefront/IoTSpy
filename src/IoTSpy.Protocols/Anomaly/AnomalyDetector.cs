using System.Collections.Concurrent;
using IoTSpy.Core.Interfaces;
using IoTSpy.Core.Models;

namespace IoTSpy.Protocols.Anomaly;

/// <summary>
/// Thread-safe statistical anomaly detector.
/// Maintains a per-host rolling baseline using Welford's online algorithm for
/// incremental mean and variance estimation, and alerts when a new observation
/// deviates more than <see cref="DeviationThreshold"/> standard deviations from
/// the established mean.
///
/// Baseline warm-up: alerts are suppressed until at least
/// <see cref="WarmUpSamples"/> observations have been collected for a host.
///
/// Request-rate anomaly: tracks request timestamps in a sliding window of
/// <see cref="RateWindowSeconds"/> seconds and alerts when the rate exceeds
/// three times the historical average.
/// </summary>
public sealed class AnomalyDetector : IAnomalyDetector
{
    /// <summary>
    /// Standard-deviation multiplier beyond which an observation is flagged.
    /// Default: 3.0 (3-sigma rule).
    /// </summary>
    public double DeviationThreshold { get; init; } = 3.0;

    /// <summary>
    /// Minimum number of samples required before anomaly alerts are emitted.
    /// Default: 30.
    /// </summary>
    public int WarmUpSamples { get; init; } = 30;

    /// <summary>
    /// Sliding window (seconds) used to compute the instantaneous request rate.
    /// Default: 60 seconds.
    /// </summary>
    public int RateWindowSeconds { get; init; } = 60;

    private readonly ConcurrentDictionary<string, HostBaseline> _baselines = new();

    /// <inheritdoc/>
    public IReadOnlyList<AnomalyAlert> Record(
        string host,
        double responseDurationMs,
        long responseSizeBytes,
        int statusCode)
    {
        var baseline = _baselines.GetOrAdd(host, h => new HostBaseline { Host = h });
        var alerts = new List<AnomalyAlert>();
        var now = DateTimeOffset.UtcNow;

        lock (baseline)
        {
            baseline.SampleCount++;

            // ── Welford update for duration ──────────────────────────────────
            var dDelta = responseDurationMs - baseline.DurationMean;
            baseline.DurationMean += dDelta / baseline.SampleCount;
            var dDelta2 = responseDurationMs - baseline.DurationMean;
            baseline.DurationM2 += dDelta * dDelta2;

            // ── Welford update for size ──────────────────────────────────────
            var sDelta = responseSizeBytes - baseline.SizeMean;
            baseline.SizeMean += sDelta / baseline.SampleCount;
            var sDelta2 = responseSizeBytes - baseline.SizeMean;
            baseline.SizeM2 += sDelta * sDelta2;

            // ── Status code distribution ─────────────────────────────────────
            baseline.StatusCodeCounts.TryGetValue(statusCode, out var prevCount);
            baseline.StatusCodeCounts[statusCode] = prevCount + 1;

            // ── Request rate (sliding window) ────────────────────────────────
            baseline.RequestTimestamps.Enqueue(now);
            var windowStart = now.AddSeconds(-RateWindowSeconds);
            while (baseline.RequestTimestamps.TryPeek(out var oldest) && oldest < windowStart)
                baseline.RequestTimestamps.Dequeue();

            // Only emit alerts after warm-up
            if (baseline.SampleCount < WarmUpSamples)
                return alerts;

            // ── Duration anomaly ─────────────────────────────────────────────
            var dStd = baseline.DurationStdDev;
            if (dStd > 0)
            {
                var dDev = Math.Abs(responseDurationMs - baseline.DurationMean) / dStd;
                if (dDev >= DeviationThreshold)
                {
                    alerts.Add(new AnomalyAlert
                    {
                        Host = host,
                        AlertType = "ResponseTime",
                        ExpectedValue = baseline.DurationMean,
                        ActualValue = responseDurationMs,
                        DeviationFactor = dDev,
                        DetectedAt = now
                    });
                }
            }

            // ── Size anomaly ─────────────────────────────────────────────────
            var sStd = baseline.SizeStdDev;
            if (sStd > 0)
            {
                var sDev = Math.Abs(responseSizeBytes - baseline.SizeMean) / sStd;
                if (sDev >= DeviationThreshold)
                {
                    alerts.Add(new AnomalyAlert
                    {
                        Host = host,
                        AlertType = "ResponseSize",
                        ExpectedValue = baseline.SizeMean,
                        ActualValue = responseSizeBytes,
                        DeviationFactor = sDev,
                        DetectedAt = now
                    });
                }
            }

            // ── Status code anomaly ──────────────────────────────────────────
            // Alert when an unexpected status class appears (e.g. a 5xx when
            // the baseline is overwhelmingly 2xx).  We require the dominant code
            // to hold ≥ 90 % of historical observations before flagging, so that
            // a genuinely mixed-status baseline doesn't produce false positives.
            var dominant = baseline.DominantStatusCode;
            if (dominant > 0 && statusCode / 100 != dominant / 100)
            {
                baseline.StatusCodeCounts.TryGetValue(dominant, out var dominantCount);
                var dominantShare = (double)dominantCount / baseline.SampleCount;
                if (dominantShare >= 0.90)
                {
                    alerts.Add(new AnomalyAlert
                    {
                        Host = host,
                        AlertType = "StatusCode",
                        ExpectedValue = dominant,
                        ActualValue = statusCode,
                        DeviationFactor = Math.Abs(statusCode - dominant) / 100.0,
                        DetectedAt = now
                    });
                }
            }

            // ── Request rate anomaly ─────────────────────────────────────────
            // Compare current window rate against historical average rate.
            // Historical average = total samples / elapsed seconds since first observation.
            var windowCount = baseline.RequestTimestamps.Count;
            var currentRate = windowCount / (double)RateWindowSeconds;   // requests / second

            // Historical rate proxy: mean samples per window
            var historicalRate = baseline.SampleCount / (double)Math.Max(
                1, (now - (baseline.RequestTimestamps.TryPeek(out var first) ? first : now)).TotalSeconds
                   + RateWindowSeconds);

            if (historicalRate > 0 && currentRate > historicalRate * (1 + DeviationThreshold))
            {
                alerts.Add(new AnomalyAlert
                {
                    Host = host,
                    AlertType = "RequestRate",
                    ExpectedValue = historicalRate,
                    ActualValue = currentRate,
                    DeviationFactor = currentRate / historicalRate,
                    DetectedAt = now
                });
            }
        }

        return alerts;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, HostBaseline> GetBaselines()
        => _baselines;

    /// <inheritdoc/>
    public void Reset(string host) => _baselines.TryRemove(host, out _);
}
