namespace IoTSpy.Core.Interfaces;

/// <summary>
/// Decodes raw bytes from a captured stream into a structured protocol message.
/// </summary>
/// <typeparam name="T">The decoded message type.</typeparam>
public interface IProtocolDecoder<T>
{
    /// <summary>
    /// Attempts to decode one or more protocol messages from the given buffer.
    /// </summary>
    /// <param name="data">Raw bytes from the network stream.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Decoded messages found in the buffer.</returns>
    Task<IReadOnlyList<T>> DecodeAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);

    /// <summary>
    /// Returns true if the first bytes look like this protocol (sniffing).
    /// </summary>
    bool CanDecode(ReadOnlySpan<byte> header);
}
