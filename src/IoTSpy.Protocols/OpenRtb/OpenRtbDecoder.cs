using System.Text;
using System.Text.Json;
using IoTSpy.Core.Enums;
using IoTSpy.Core.Interfaces;

namespace IoTSpy.Protocols.OpenRtb;

/// <summary>
/// Decodes OpenRTB 2.x bid request and bid response JSON payloads.
/// Detects PII-bearing fields for downstream stripping.
/// </summary>
public sealed class OpenRtbDecoder : IProtocolDecoder<OpenRtbMessage>
{
    private static readonly byte[] ImpKey = "\"imp\""u8.ToArray();
    private static readonly byte[] SeatBidKey = "\"seatbid\""u8.ToArray();

    private static readonly HashSet<string> PiiDeviceFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "ip", "ipv6", "ifa", "didsha1", "didmd5", "macsha1", "macmd5", "ua"
    };

    public bool CanDecode(ReadOnlySpan<byte> header)
    {
        if (header.Length < 2) return false;

        var trimmed = header.TrimStart((byte)' ');
        if (trimmed.IsEmpty || trimmed[0] != (byte)'{') return false;

        var sample = header[..Math.Min(header.Length, 512)];
        return ContainsSequence(sample, ImpKey) || ContainsSequence(sample, SeatBidKey);
    }

    public Task<IReadOnlyList<OpenRtbMessage>> DecodeAsync(
        ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var results = new List<OpenRtbMessage>();
        var json = Encoding.UTF8.GetString(data.Span);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var isBidRequest = root.TryGetProperty("imp", out var impArr)
                               && impArr.ValueKind == JsonValueKind.Array;
            var isBidResponse = root.TryGetProperty("seatbid", out var seatBidArr)
                                && seatBidArr.ValueKind == JsonValueKind.Array;

            if (!isBidRequest && !isBidResponse)
                return Task.FromResult<IReadOnlyList<OpenRtbMessage>>(results);

            var piiFields = new List<string>();
            var version = root.TryGetProperty("ver", out var ver) ? ver.GetString() ?? "" : "";
            var auctionId = root.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "";

            if (isBidRequest)
            {
                var impressions = ParseImpressions(impArr);
                var device = ParseDevice(root, piiFields);
                var user = ParseUser(root, piiFields);
                var publisher = ParsePublisher(root);

                results.Add(new OpenRtbMessage
                {
                    MessageType = OpenRtbMessageType.BidRequest,
                    Version = version,
                    AuctionId = auctionId,
                    Impressions = impressions,
                    Device = device,
                    User = user,
                    Publisher = publisher,
                    PiiFieldsDetected = piiFields,
                    RawJson = json.Length > 8192 ? json[..8192] : json
                });
            }
            else if (isBidResponse)
            {
                var bids = ParseBids(seatBidArr);

                results.Add(new OpenRtbMessage
                {
                    MessageType = OpenRtbMessageType.BidResponse,
                    Version = version,
                    AuctionId = auctionId,
                    Bids = bids,
                    PiiFieldsDetected = piiFields,
                    RawJson = json.Length > 8192 ? json[..8192] : json
                });
            }
        }
        catch (JsonException)
        {
            // Malformed JSON — return empty
        }

        return Task.FromResult<IReadOnlyList<OpenRtbMessage>>(results);
    }

    private static List<OpenRtbImpression> ParseImpressions(JsonElement impArr)
    {
        var result = new List<OpenRtbImpression>();

        foreach (var imp in impArr.EnumerateArray())
        {
            var impId = imp.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            int? bannerW = null, bannerH = null;
            string? videoMimes = null;

            if (imp.TryGetProperty("banner", out var banner))
            {
                if (banner.TryGetProperty("w", out var w)) bannerW = w.GetInt32();
                if (banner.TryGetProperty("h", out var h)) bannerH = h.GetInt32();
            }

            if (imp.TryGetProperty("video", out var video)
                && video.TryGetProperty("mimes", out var mimes)
                && mimes.ValueKind == JsonValueKind.Array)
            {
                videoMimes = string.Join(",", mimes.EnumerateArray().Select(m => m.GetString()));
            }

            double? bidFloor = imp.TryGetProperty("bidfloor", out var bf) ? bf.GetDouble() : null;
            var bidFloorCur = imp.TryGetProperty("bidfloorcur", out var bfc) ? bfc.GetString() : null;

            result.Add(new OpenRtbImpression(impId, bannerW, bannerH, videoMimes, bidFloor, bidFloorCur));
        }

        return result;
    }

    private static OpenRtbDeviceInfo? ParseDevice(JsonElement root, List<string> piiFields)
    {
        if (!root.TryGetProperty("device", out var device))
            return null;

        string? ip = null, ipv6 = null, ifa = null, ua = null;
        string? make = null, model = null, os = null;
        double? geoLat = null, geoLon = null;
        string? geoCountry = null, geoMetro = null;

        foreach (var prop in device.EnumerateObject())
        {
            switch (prop.Name)
            {
                case "ip": ip = prop.Value.GetString(); piiFields.Add("device.ip"); break;
                case "ipv6": ipv6 = prop.Value.GetString(); piiFields.Add("device.ipv6"); break;
                case "ifa": ifa = prop.Value.GetString(); piiFields.Add("device.ifa"); break;
                case "ua": ua = prop.Value.GetString(); piiFields.Add("device.ua"); break;
                case "make": make = prop.Value.GetString(); break;
                case "model": model = prop.Value.GetString(); break;
                case "os": os = prop.Value.GetString(); break;
                case "didsha1": piiFields.Add("device.didsha1"); break;
                case "didmd5": piiFields.Add("device.didmd5"); break;
                case "macsha1": piiFields.Add("device.macsha1"); break;
                case "macmd5": piiFields.Add("device.macmd5"); break;
                case "geo":
                    if (prop.Value.TryGetProperty("lat", out var lat))
                    {
                        geoLat = lat.GetDouble();
                        piiFields.Add("device.geo.lat");
                    }
                    if (prop.Value.TryGetProperty("lon", out var lon))
                    {
                        geoLon = lon.GetDouble();
                        piiFields.Add("device.geo.lon");
                    }
                    if (prop.Value.TryGetProperty("country", out var country))
                        geoCountry = country.GetString();
                    if (prop.Value.TryGetProperty("metro", out var metro))
                        geoMetro = metro.GetString();
                    break;
            }
        }

        return new OpenRtbDeviceInfo(ip, ipv6, ifa, ua, geoLat, geoLon, geoCountry, geoMetro, make, model, os);
    }

    private static OpenRtbUserInfo? ParseUser(JsonElement root, List<string> piiFields)
    {
        if (!root.TryGetProperty("user", out var user))
            return null;

        string? id = null, buyerUid = null, keywords = null;
        var segmentCount = 0;

        if (user.TryGetProperty("id", out var idEl))
        {
            id = idEl.GetString();
            piiFields.Add("user.id");
        }

        if (user.TryGetProperty("buyeruid", out var buid))
        {
            buyerUid = buid.GetString();
            piiFields.Add("user.buyeruid");
        }

        if (user.TryGetProperty("keywords", out var kw))
        {
            keywords = kw.GetString();
            piiFields.Add("user.keywords");
        }

        if (user.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            piiFields.Add("user.data");
            foreach (var d in data.EnumerateArray())
            {
                if (d.TryGetProperty("segment", out var seg) && seg.ValueKind == JsonValueKind.Array)
                    segmentCount += seg.GetArrayLength();
            }
        }

        return new OpenRtbUserInfo(id, buyerUid, keywords, segmentCount);
    }

    private static OpenRtbPublisherInfo? ParsePublisher(JsonElement root)
    {
        JsonElement context;
        if (root.TryGetProperty("site", out context) || root.TryGetProperty("app", out context))
        {
            var domain = context.TryGetProperty("domain", out var d) ? d.GetString() : null;
            var bundle = context.TryGetProperty("bundle", out var b) ? b.GetString() : null;
            string? pubId = null, pubName = null;

            if (context.TryGetProperty("publisher", out var pub))
            {
                pubId = pub.TryGetProperty("id", out var pid) ? pid.GetString() : null;
                pubName = pub.TryGetProperty("name", out var pn) ? pn.GetString() : null;
            }

            return new OpenRtbPublisherInfo(domain, bundle, pubId, pubName);
        }

        return null;
    }

    private static List<OpenRtbBid> ParseBids(JsonElement seatBidArr)
    {
        var result = new List<OpenRtbBid>();

        foreach (var seatbid in seatBidArr.EnumerateArray())
        {
            if (!seatbid.TryGetProperty("bid", out var bidArr) || bidArr.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var bid in bidArr.EnumerateArray())
            {
                var impId = bid.TryGetProperty("impid", out var imp) ? imp.GetString() ?? "" : "";
                var price = bid.TryGetProperty("price", out var p) ? p.GetDouble() : 0;
                var adm = bid.TryGetProperty("adm", out var a) ? a.GetString() : null;
                var crid = bid.TryGetProperty("crid", out var c) ? c.GetString() : null;
                string? adomain = null;

                if (bid.TryGetProperty("adomain", out var ad) && ad.ValueKind == JsonValueKind.Array)
                    adomain = string.Join(",", ad.EnumerateArray().Select(x => x.GetString()));

                // Truncate adm for storage
                if (adm is { Length: > 1024 })
                    adm = adm[..1024];

                result.Add(new OpenRtbBid(impId, price, adm, crid, adomain));
            }
        }

        return result;
    }

    private static bool ContainsSequence(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        return haystack.IndexOf(needle) >= 0;
    }
}
