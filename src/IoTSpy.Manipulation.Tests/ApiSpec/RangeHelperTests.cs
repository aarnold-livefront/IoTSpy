using IoTSpy.Manipulation.ApiSpec;
using Xunit;

namespace IoTSpy.Manipulation.Tests.ApiSpec;

public class RangeHelperTests
{
    [Fact]
    public void TryParse_ExplicitStartEnd_ParsesRange()
    {
        Assert.True(RangeHelper.TryParse("bytes=0-99", 1000, out var r));
        Assert.Equal(0, r.Start);
        Assert.Equal(99, r.End);
        Assert.Equal(100, r.Length);
        Assert.Equal(1000, r.TotalLength);
    }

    [Fact]
    public void TryParse_StartOnly_ExtendsToEnd()
    {
        Assert.True(RangeHelper.TryParse("bytes=500-", 1000, out var r));
        Assert.Equal(500, r.Start);
        Assert.Equal(999, r.End);
    }

    [Fact]
    public void TryParse_SuffixOnly_ReturnsLastNBytes()
    {
        Assert.True(RangeHelper.TryParse("bytes=-200", 1000, out var r));
        Assert.Equal(800, r.Start);
        Assert.Equal(999, r.End);
    }

    [Fact]
    public void TryParse_EndExceedsFileLength_Clamps()
    {
        Assert.True(RangeHelper.TryParse("bytes=100-9999", 1000, out var r));
        Assert.Equal(100, r.Start);
        Assert.Equal(999, r.End);
    }

    [Fact]
    public void TryParse_SuffixLargerThanFile_ReturnsFullFile()
    {
        Assert.True(RangeHelper.TryParse("bytes=-5000", 1000, out var r));
        Assert.Equal(0, r.Start);
        Assert.Equal(999, r.End);
    }

    [Fact]
    public void TryParse_StartExceedsLength_Fails()
    {
        Assert.False(RangeHelper.TryParse("bytes=2000-3000", 1000, out _));
    }

    [Fact]
    public void TryParse_EndBeforeStart_Fails()
    {
        Assert.False(RangeHelper.TryParse("bytes=500-100", 1000, out _));
    }

    [Fact]
    public void TryParse_MultiRange_Fails()
    {
        Assert.False(RangeHelper.TryParse("bytes=0-99,200-299", 1000, out _));
    }

    [Theory]
    [InlineData("bytes=")]
    [InlineData("bytes=abc-def")]
    [InlineData("pages=0-99")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_Malformed_Fails(string? header)
    {
        Assert.False(RangeHelper.TryParse(header, 1000, out _));
    }

    [Fact]
    public void TryParse_ZeroFileLength_Fails()
    {
        Assert.False(RangeHelper.TryParse("bytes=0-99", 0, out _));
    }
}
