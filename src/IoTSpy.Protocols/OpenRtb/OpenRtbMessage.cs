using IoTSpy.Core.Enums;

namespace IoTSpy.Protocols.OpenRtb;

public sealed class OpenRtbMessage
{
    public OpenRtbMessageType MessageType { get; init; }
    public string Version { get; init; } = string.Empty;
    public string AuctionId { get; init; } = string.Empty;
    public IReadOnlyList<OpenRtbImpression> Impressions { get; init; } = [];
    public OpenRtbDeviceInfo? Device { get; init; }
    public OpenRtbUserInfo? User { get; init; }
    public OpenRtbPublisherInfo? Publisher { get; init; }
    public IReadOnlyList<OpenRtbBid> Bids { get; init; } = [];
    public List<string> PiiFieldsDetected { get; init; } = [];
    public string RawJson { get; init; } = string.Empty;
}

public sealed record OpenRtbDeviceInfo(
    string? Ip,
    string? Ipv6,
    string? Ifa,
    string? Ua,
    double? GeoLat,
    double? GeoLon,
    string? GeoCountry,
    string? GeoMetro,
    string? Make,
    string? Model,
    string? Os);

public sealed record OpenRtbUserInfo(
    string? Id,
    string? BuyerUid,
    string? Keywords,
    int DataSegmentCount);

public sealed record OpenRtbPublisherInfo(
    string? Domain,
    string? Bundle,
    string? PublisherId,
    string? PublisherName);

public sealed record OpenRtbImpression(
    string Id,
    int? BannerW,
    int? BannerH,
    string? VideoMimes,
    double? BidFloor,
    string? BidFloorCur);

public sealed record OpenRtbBid(
    string ImpId,
    double Price,
    string? Adm,
    string? Crid,
    string? Adomain);
