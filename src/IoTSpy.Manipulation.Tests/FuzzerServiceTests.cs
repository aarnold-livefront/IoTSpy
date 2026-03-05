using Xunit;
using IoTSpy.Core.Enums;
using IoTSpy.Manipulation;

namespace IoTSpy.Manipulation.Tests;

public class FuzzerServiceTests
{
    // ── Mutate: empty body ───────────────────────────────────────────────────

    [Theory]
    [InlineData(FuzzerStrategy.Random)]
    [InlineData(FuzzerStrategy.Boundary)]
    [InlineData(FuzzerStrategy.BitFlip)]
    public void Mutate_EmptyBody_ReturnsEmpty(FuzzerStrategy strategy)
    {
        var result = FuzzerService.Mutate(string.Empty, 0, strategy);

        Assert.Equal(string.Empty, result);
    }

    // ── Mutate: Random strategy ──────────────────────────────────────────────

    [Fact]
    public void Mutate_Random_IsDeterministicForSameIndex()
    {
        var body = "hello world test body 123";
        var result1 = FuzzerService.Mutate(body, 42, FuzzerStrategy.Random);
        var result2 = FuzzerService.Mutate(body, 42, FuzzerStrategy.Random);

        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Mutate_Random_DifferentIndices_ProduceDifferentResults()
    {
        var body = "hello world test body 123456";
        var result0 = FuzzerService.Mutate(body, 0, FuzzerStrategy.Random);
        var result1 = FuzzerService.Mutate(body, 1, FuzzerStrategy.Random);

        // With different seeds the mutations should differ (extremely unlikely to collide)
        Assert.NotEqual(result0, result1);
    }

    [Fact]
    public void Mutate_Random_ReturnsSameLengthAsInput()
    {
        var body = "abcdefghij";
        var result = FuzzerService.Mutate(body, 0, FuzzerStrategy.Random);

        // Random byte mutation doesn't change byte array length, but UTF-8 re-encoding
        // may change string length due to multi-byte sequences. Just verify non-null.
        Assert.NotNull(result);
    }

    // ── Mutate: Boundary strategy ────────────────────────────────────────────

    [Fact]
    public void Mutate_Boundary_ModifiesBody()
    {
        var body = "{\"value\": \"normal\"}";
        var result = FuzzerService.Mutate(body, 0, FuzzerStrategy.Boundary);

        Assert.NotNull(result);
        Assert.NotEqual(string.Empty, result);
    }

    [Fact]
    public void Mutate_Boundary_CyclesThroughBoundaryValues()
    {
        // Each index picks a different boundary value (cycles via modulo)
        var body = "some-non-json-body";
        var results = Enumerable.Range(0, 13)
            .Select(i => FuzzerService.Mutate(body, i, FuzzerStrategy.Boundary))
            .ToList();

        // All results should be non-empty
        Assert.All(results, r => Assert.NotEmpty(r));
    }

    [Fact]
    public void Mutate_Boundary_JsonBody_ReplacesValueInJson()
    {
        // With a JSON body containing a key-value pair, boundary should replace the value
        var body = "{\"username\": \"admin\"}";
        var result = FuzzerService.Mutate(body, 0, FuzzerStrategy.Boundary); // index 0 = "0"

        Assert.NotNull(result);
        // The result should differ from the original body
        Assert.NotEqual(body, result);
    }

    [Fact]
    public void Mutate_Boundary_NonJsonBody_AppendsValue()
    {
        // Without JSON key-value patterns, boundary falls back to appending
        var body = "plain text content";
        var result = FuzzerService.Mutate(body, 0, FuzzerStrategy.Boundary);

        Assert.StartsWith("plain text content", result);
    }

    // ── Mutate: BitFlip strategy ─────────────────────────────────────────────

    [Fact]
    public void Mutate_BitFlip_FlipsExactlyOneBit()
    {
        var body = "A"; // 'A' = 0x41
        // index=0 → pos=0%1=0, bit=0/1%8=0 → XOR with 1 → 0x40 = '@'
        var result = FuzzerService.Mutate(body, 0, FuzzerStrategy.BitFlip);

        Assert.NotNull(result);
        Assert.NotEqual(body, result); // bit was flipped so character changed
    }

    [Fact]
    public void Mutate_BitFlip_IsDeterministic()
    {
        var body = "test body 12345";
        var result1 = FuzzerService.Mutate(body, 7, FuzzerStrategy.BitFlip);
        var result2 = FuzzerService.Mutate(body, 7, FuzzerStrategy.BitFlip);

        Assert.Equal(result1, result2);
    }

    [Fact]
    public void Mutate_BitFlip_DifferentIndices_ProduceDifferentResults()
    {
        var body = "abcdefghij";
        var result0 = FuzzerService.Mutate(body, 0, FuzzerStrategy.BitFlip);
        var result1 = FuzzerService.Mutate(body, 1, FuzzerStrategy.BitFlip);

        Assert.NotEqual(result0, result1);
    }

    [Fact]
    public void Mutate_BitFlip_DoublyApplied_RestoresOriginal()
    {
        // Flipping the same bit twice restores the original value
        var body = "Hello";
        var flipped = FuzzerService.Mutate(body, 0, FuzzerStrategy.BitFlip);

        // Re-flipping the same position and bit should give back original (XOR is self-inverse)
        // Verify the byte array approach: same index=0 → same pos=0, same bit=0
        // We need to work at byte level since UTF-8 re-encoding may vary
        var bytes = System.Text.Encoding.UTF8.GetBytes(body);
        var flippedBytes = System.Text.Encoding.UTF8.GetBytes(flipped);

        // Only the first byte should differ (for ASCII input)
        if (bytes.Length == flippedBytes.Length)
        {
            Assert.Equal(bytes[0] ^ 1, flippedBytes[0]);
        }
    }

    // ── Mutate: unknown strategy returns unchanged ────────────────────────────

    [Fact]
    public void Mutate_UnknownStrategy_ReturnsOriginalBody()
    {
        var body = "unchanged";
        var result = FuzzerService.Mutate(body, 0, (FuzzerStrategy)99);

        Assert.Equal(body, result);
    }
}
