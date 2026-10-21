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

    // Faz 2 komisyon/bloke (basit model; tam matris — kart tipi/MCC/ciro, Ders 9 — sonraki faz).
    public Dictionary<int, decimal> CommissionRates { get; set; } = new(); // taksit → oran
    public decimal DefaultCommissionRate { get; set; }                     // tabloda yoksa
    public int BlokeGun { get; set; }                                      // valör gün sayısı

    /// <summary>Taksit için komisyon oranı (tabloda yoksa varsayılan).</summary>
    public decimal RateFor(int installment)
        => CommissionRates.TryGetValue(installment, out var r) ? r : DefaultCommissionRate;
}
