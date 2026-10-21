namespace MaviBank.Api.Domains.Merchants;

/// <summary>
/// Üye-işyeri protokol kimliği — salt-okur düz Marten dokümanı (seed; onboarding Faz 7). Kimlik = `ShopCode`.
/// Credential store başına FARKLIDIR. `SecretKey` SIR (3DS hash Faz 4) — cevap/log/DTO'da görünmez (İlke VIII).
/// </summary>
public class Merchant
{
    public string ShopCode { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty; // onboarding/sahibi banka (MAVIBANK/SARIBANK...) — etiket
    public string UserCode { get; set; } = string.Empty;
    public string UserPass { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string SubscriptionKey { get; set; } = string.Empty;

    // Faz 2 komisyon/bloke (basit model; tam matris — kart tipi/MCC/ciro, Ders 9 — sonraki faz).
    public Dictionary<int, decimal> CommissionRates { get; set; } = new(); // taksit → oran
    public decimal DefaultCommissionRate { get; set; }                     // tabloda yoksa
    public int BlokeGun { get; set; }                                      // valör gün sayısı

    // Faz 7 panel: izinli IP listesi (boş = kısıt yok) + SecretKey rotasyonu (eski anahtar 24s geçerli).
    public string[] AllowedIps { get; set; } = Array.Empty<string>();
    public string? PreviousSecretKey { get; set; }
    public DateTime? PreviousSecretKeyValidUntil { get; set; }

    /// <summary>Taksit için komisyon oranı (tabloda yoksa varsayılan).</summary>
    public decimal RateFor(int installment)
        => CommissionRates.TryGetValue(installment, out var r) ? r : DefaultCommissionRate;

    /// <summary>İzinli IP mi — liste boşsa kısıt yok (whitelist dışı istek reddi, Faz 7).</summary>
    public bool IsIpAllowed(string? ip)
        => AllowedIps.Length == 0 || (ip is not null && AllowedIps.Contains(ip));

    /// <summary>Panel "Yenile": yeni SecretKey; eski anahtar overlap (24s) boyunca geçerli kalır.</summary>
    public void ReissueSecretKey(string newKey, DateTime now, TimeSpan overlap)
    {
        PreviousSecretKey = SecretKey;
        PreviousSecretKeyValidUntil = now + overlap;
        SecretKey = newKey;
    }

    /// <summary>3D hash için geçerli imza anahtarları: güncel + (overlap içinde) önceki.</summary>
    public IEnumerable<string> ValidSecretKeys(DateTime now)
    {
        yield return SecretKey;
        if (PreviousSecretKey is not null && PreviousSecretKeyValidUntil is { } v && now < v)
            yield return PreviousSecretKey;
    }
}
