namespace IoTSpy.Core.Models;

public class ProtoSchema
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string RawProto { get; set; } = string.Empty;

    // Serialised JSON: { "MessageName": { "1": "field_name", ... }, ... }
    public string FieldMapJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
