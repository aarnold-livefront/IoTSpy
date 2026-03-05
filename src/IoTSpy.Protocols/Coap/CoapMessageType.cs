namespace IoTSpy.Protocols.Coap;

/// <summary>
/// CoAP message types per RFC 7252 §3.
/// </summary>
public enum CoapMessageType : byte
{
    Confirmable = 0,      // CON
    NonConfirmable = 1,   // NON
    Acknowledgement = 2,  // ACK
    Reset = 3             // RST
}
