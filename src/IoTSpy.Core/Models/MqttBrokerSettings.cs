namespace IoTSpy.Core.Models;

public class MqttBrokerSettings
{
    public bool Enabled { get; set; }
    public int ListenPort { get; set; } = 1883;
    public string ListenAddress { get; set; } = "0.0.0.0";
    public string? UpstreamHost { get; set; }
    public int UpstreamPort { get; set; } = 1883;
    public bool LogPayloads { get; set; } = true;
    public List<string> TopicFilters { get; set; } = [];
}
