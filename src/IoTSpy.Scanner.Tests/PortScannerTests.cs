using IoTSpy.Scanner;

namespace IoTSpy.Scanner.Tests;

public class PortScannerTests
{
    // ── ParsePortRange ───────────────────────────────────────────────────────

    [Fact]
    public void ParsePortRange_SinglePort_ReturnsOnePort()
    {
        var ports = PortScanner.ParsePortRange("80");

        Assert.Single(ports);
        Assert.Equal(80, ports[0]);
    }

    [Fact]
    public void ParsePortRange_Range_ReturnsAllPorts()
    {
        var ports = PortScanner.ParsePortRange("20-25");

        Assert.Equal(6, ports.Count);
        Assert.Equal([20, 21, 22, 23, 24, 25], ports);
    }

    [Fact]
    public void ParsePortRange_MultipleSinglePorts_ReturnsAllSorted()
    {
        var ports = PortScanner.ParsePortRange("443,80,22");

        Assert.Equal(3, ports.Count);
        Assert.Equal([22, 80, 443], ports);
    }

    [Fact]
    public void ParsePortRange_MixedRangeAndSingle_ReturnsAllSorted()
    {
        var ports = PortScanner.ParsePortRange("80,443,8080-8082");

        Assert.Equal(5, ports.Count);
        Assert.Equal([80, 443, 8080, 8081, 8082], ports);
    }

    [Fact]
    public void ParsePortRange_DuplicatePorts_ReturnsDistinct()
    {
        var ports = PortScanner.ParsePortRange("80,80,80");

        Assert.Single(ports);
        Assert.Equal(80, ports[0]);
    }

    [Fact]
    public void ParsePortRange_EmptyString_ReturnsEmpty()
    {
        var ports = PortScanner.ParsePortRange("");

        Assert.Empty(ports);
    }

    [Fact]
    public void ParsePortRange_InvalidEntry_IgnoresInvalid()
    {
        var ports = PortScanner.ParsePortRange("80,abc,443");

        Assert.Equal(2, ports.Count);
        Assert.Contains(80, ports);
        Assert.Contains(443, ports);
    }

    [Fact]
    public void ParsePortRange_ZeroPort_IsExcluded()
    {
        // Port 0 is not in 1-65535, should be excluded
        var ports = PortScanner.ParsePortRange("0");

        Assert.Empty(ports);
    }

    [Fact]
    public void ParsePortRange_PortAboveMax_IsExcluded()
    {
        // Port 65536 is out of range
        var ports = PortScanner.ParsePortRange("65536");

        Assert.Empty(ports);
    }

    [Fact]
    public void ParsePortRange_MaxValidPort_IsIncluded()
    {
        var ports = PortScanner.ParsePortRange("65535");

        Assert.Single(ports);
        Assert.Equal(65535, ports[0]);
    }

    [Fact]
    public void ParsePortRange_RangeStartsBelowMin_ClampsToOne()
    {
        // Range 0-5 should clamp start to 1
        var ports = PortScanner.ParsePortRange("0-5");

        Assert.Equal(5, ports.Count);
        Assert.Equal(1, ports[0]);
        Assert.Equal(5, ports[^1]);
    }

    [Fact]
    public void ParsePortRange_WellKnownIotPorts_AllPresent()
    {
        var ports = PortScanner.ParsePortRange("1883,5683,8883");

        Assert.Equal(3, ports.Count);
        Assert.Contains(1883, ports); // MQTT
        Assert.Contains(5683, ports); // CoAP
        Assert.Contains(8883, ports); // MQTT-TLS
    }

    [Fact]
    public void ParsePortRange_WhitespaceAroundPorts_Parsed()
    {
        var ports = PortScanner.ParsePortRange(" 80 , 443 ");

        Assert.Equal(2, ports.Count);
        Assert.Contains(80, ports);
        Assert.Contains(443, ports);
    }

    [Fact]
    public void ParsePortRange_LargeRange_ReturnsCorrectCount()
    {
        var ports = PortScanner.ParsePortRange("1-100");

        Assert.Equal(100, ports.Count);
        Assert.Equal(1, ports[0]);
        Assert.Equal(100, ports[^1]);
    }
}
