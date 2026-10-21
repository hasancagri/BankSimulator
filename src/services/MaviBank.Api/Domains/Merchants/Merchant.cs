namespace MaviBank.Api.Domains.Merchants;

/// <summary>
/// Üye-işyeri protokol kimliği — salt-okur düz Marten dokümanı (seed; onboarding Faz 7). Kimlik = `ShopCode`.
/// Credential store başına FARKLIDIR. `SecretKey` SIR (3DS hash Faz 4) — cevap/log/DTO'da görünmez (İlke VIII).
/// </summary>
public class Merchant
{
    public string ShopCode { get; set; } = string.Empty;
    public string UserCode { get; set; } = string.Empty;
    public string UserPass { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string SubscriptionKey { get; set; } = string.Empty;
}
