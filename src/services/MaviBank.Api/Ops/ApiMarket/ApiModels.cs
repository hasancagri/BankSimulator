namespace MaviBank.Api.Ops.ApiMarket;

// Faz 3 — API Market POS operasyon yüzü zarfı (gereksinim §2). İstek: Header + Parameters[]; cevap {Data}.
// Kimlik: SubscriptionKey header (seed'li merchant'a çözülür). Tel JSON (ödeme yüzü `;;` — AYRI yüz).

public record ApiHeader(string AppKey, string Channel, string ChannelSessionId, string ChannelRequestId);

public record ApiParameter(string Name, string Value);

public record ApiRequest(ApiHeader? Header, List<ApiParameter>? Parameters)
{
    public string? Get(string name) =>
        Parameters?.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
}

public record ApiResponse(object Data)
{
    public static ApiResponse Of(object data) => new(data);
}
