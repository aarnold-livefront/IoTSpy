namespace IoTSpy.Core.Enums;

/// <summary>
/// Whether the rule applies to the request (before forwarding) or response (before returning to client).
/// </summary>
public enum ManipulationPhase
{
    Request,
    Response
}
