using System.Text;
using Xunit;
using IoTSpy.Core.Enums;
using IoTSpy.Protocols.OpenRtb;

namespace IoTSpy.Protocols.Tests;

public class OpenRtbDecoderTests
{
    private readonly OpenRtbDecoder _decoder = new();

    private static readonly string SampleBidRequest = """
        {
          "id": "auction-123",
          "ver": "2.5",
          "imp": [
            {
              "id": "imp-1",
              "banner": { "w": 300, "h": 250 },
              "bidfloor": 0.5,
              "bidfloorcur": "USD"
            }
          ],
          "device": {
            "ip": "192.168.1.42",
            "ipv6": "2001:db8::1",
            "ifa": "AEBE52E7-03EE-455A-B3C4-E57283966239",
            "ua": "Mozilla/5.0 (Linux; Android 12; Pixel 6) Chrome/108.0.5359.128 Mobile Safari/537.36",
            "geo": { "lat": 37.7749, "lon": -122.4194, "country": "US", "metro": "807" },
            "make": "Google",
            "model": "Pixel 6",
            "os": "Android",
            "didsha1": "abc123",
            "macmd5": "def456"
          },
          "user": {
            "id": "user-abc",
            "buyeruid": "buyer-xyz",
            "keywords": "sports,tech",
            "data": [
              { "segment": [{ "id": "seg1" }, { "id": "seg2" }] }
            ]
          },
          "site": {
            "domain": "example.com",
            "publisher": { "id": "pub-1", "name": "Example Publisher" }
          }
        }
        """;

    private static readonly string SampleBidResponse = """
        {
          "id": "auction-123",
          "seatbid": [
            {
              "bid": [
                {
                  "impid": "imp-1",
                  "price": 2.50,
                  "adm": "<div>ad markup here</div>",
                  "crid": "creative-1",
                  "adomain": ["advertiser.com", "brand.com"]
                }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void CanDecode_BidRequest_ReturnsTrue()
    {
        Assert.True(_decoder.CanDecode(Encoding.UTF8.GetBytes(SampleBidRequest)));
    }

    [Fact]
    public void CanDecode_BidResponse_ReturnsTrue()
    {
        Assert.True(_decoder.CanDecode(Encoding.UTF8.GetBytes(SampleBidResponse)));
    }

    [Fact]
    public void CanDecode_NonOpenRtb_ReturnsFalse()
    {
        var json = """{"foo":"bar","baz":123}""";
        Assert.False(_decoder.CanDecode(Encoding.UTF8.GetBytes(json)));
    }

    [Fact]
    public void CanDecode_Empty_ReturnsFalse()
    {
        Assert.False(_decoder.CanDecode([]));
    }

    [Fact]
    public async Task DecodeAsync_BidRequest_ExtractsImpressions()
    {
        var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(SampleBidRequest));
        Assert.Single(messages);
        var msg = messages[0];
        Assert.Equal(OpenRtbMessageType.BidRequest, msg.MessageType);
        Assert.Equal("2.5", msg.Version);
        Assert.Equal("auction-123", msg.AuctionId);
        Assert.Single(msg.Impressions);
        Assert.Equal("imp-1", msg.Impressions[0].Id);
        Assert.Equal(300, msg.Impressions[0].BannerW);
        Assert.Equal(250, msg.Impressions[0].BannerH);
        Assert.Equal(0.5, msg.Impressions[0].BidFloor);
    }

    [Fact]
    public async Task DecodeAsync_BidRequest_ExtractsDeviceInfo()
    {
        var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(SampleBidRequest));
        var device = messages[0].Device;
        Assert.NotNull(device);
        Assert.Equal("192.168.1.42", device.Ip);
        Assert.Equal("2001:db8::1", device.Ipv6);
        Assert.Equal("AEBE52E7-03EE-455A-B3C4-E57283966239", device.Ifa);
        Assert.Equal(37.7749, device.GeoLat);
        Assert.Equal(-122.4194, device.GeoLon);
        Assert.Equal("US", device.GeoCountry);
        Assert.Equal("Google", device.Make);
        Assert.Equal("Pixel 6", device.Model);
        Assert.Equal("Android", device.Os);
    }

    [Fact]
    public async Task DecodeAsync_BidRequest_ExtractsUserInfo()
    {
        var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(SampleBidRequest));
        var user = messages[0].User;
        Assert.NotNull(user);
        Assert.Equal("user-abc", user.Id);
        Assert.Equal("buyer-xyz", user.BuyerUid);
        Assert.Equal("sports,tech", user.Keywords);
        Assert.Equal(2, user.DataSegmentCount);
    }

    [Fact]
    public async Task DecodeAsync_BidRequest_DetectsPiiFields()
    {
        var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(SampleBidRequest));
        var pii = messages[0].PiiFieldsDetected;
        Assert.Contains("device.ip", pii);
        Assert.Contains("device.ipv6", pii);
        Assert.Contains("device.ifa", pii);
        Assert.Contains("device.ua", pii);
        Assert.Contains("device.geo.lat", pii);
        Assert.Contains("device.geo.lon", pii);
        Assert.Contains("device.didsha1", pii);
        Assert.Contains("device.macmd5", pii);
        Assert.Contains("user.id", pii);
        Assert.Contains("user.buyeruid", pii);
        Assert.Contains("user.keywords", pii);
        Assert.Contains("user.data", pii);
    }

    [Fact]
    public async Task DecodeAsync_BidRequest_ExtractsPublisher()
    {
        var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(SampleBidRequest));
        var pub = messages[0].Publisher;
        Assert.NotNull(pub);
        Assert.Equal("example.com", pub.Domain);
        Assert.Equal("pub-1", pub.PublisherId);
        Assert.Equal("Example Publisher", pub.PublisherName);
    }

    [Fact]
    public async Task DecodeAsync_BidResponse_ExtractsBids()
    {
        var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(SampleBidResponse));
        Assert.Single(messages);
        var msg = messages[0];
        Assert.Equal(OpenRtbMessageType.BidResponse, msg.MessageType);
        Assert.Single(msg.Bids);
        Assert.Equal("imp-1", msg.Bids[0].ImpId);
        Assert.Equal(2.50, msg.Bids[0].Price);
        Assert.Equal("creative-1", msg.Bids[0].Crid);
        Assert.Equal("advertiser.com,brand.com", msg.Bids[0].Adomain);
    }

    [Fact]
    public async Task DecodeAsync_MalformedJson_ReturnsEmpty()
    {
        var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes("{invalid json"));
        Assert.Empty(messages);
    }

    [Fact]
    public async Task DecodeAsync_BidRequest_TruncatesRawJson()
    {
        // Create a large JSON payload > 8192 bytes
        var bigJson = """{"id":"test","imp":[{"id":"1"}],"extra":""" + "\"" + new string('x', 10000) + "\"}";
        var messages = await _decoder.DecodeAsync(Encoding.UTF8.GetBytes(bigJson));
        Assert.Single(messages);
        Assert.True(messages[0].RawJson.Length <= 8192);
    }
}
