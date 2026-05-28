namespace IoTSpy.Analytics;

public sealed class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    public bool Enabled { get; set; } = true;
    public int BatchIntervalMinutes { get; set; } = 15;
    public int BatchSize { get; set; } = 1000;
    public string ModelPath { get; set; } = "models/traffic_classifier_v1.onnx";
}
