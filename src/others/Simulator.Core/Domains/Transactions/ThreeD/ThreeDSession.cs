namespace Simulator.Core.Domains.Transactions.ThreeD;

/// <summary>
/// Bekleyen 3D Secure oturumu — düz Marten doküman (init ↔ ACS kod girişi arası durum). Tam PAN SAKLANMAZ
/// (yalnız MaskedPan + kart senaryosu; İlke VI/VIII). Tek-kullanım: ACS tamamlanınca Completed. Kimlik = Sid.
/// </summary>
public class ThreeDSession
{
    private ThreeDSession() { }

    public string Sid { get; private set; } = string.Empty;
    public string ShopCode { get; private set; } = string.Empty;
    public string OrderId { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public int Currency { get; private set; }
    public int Installment { get; private set; }
    public int CardType { get; private set; }
    public string MaskedPan { get; private set; } = string.Empty;
    public string OkUrl { get; private set; } = string.Empty;
    public string FailUrl { get; private set; } = string.Empty;
    public string Rnd { get; private set; } = string.Empty;
    public string SecureType { get; private set; } = string.Empty; // 3DModel / 3DPay / 3DHost
    public CardScenario Scenario { get; private set; } // 3D sonucu + çekim kararını belirler (determinist)
    public ThreeDSessionStatus Status { get; private set; }
    public string? Md { get; private set; }            // 3DModel: ACS sonrası üretilir, completion doğrular
    public bool ThreeDPassed { get; private set; }     // 3D doğrulama sonucu (completion çekimi için)
    public DateTime CreatedAt { get; private set; }

    public static ThreeDSession Create(string shopCode, string orderId, decimal amount, int currency,
        int installment, int cardType, string maskedPan, string okUrl, string failUrl, string rnd,
        string secureType, CardScenario scenario, DateTime now)
        => new()
        {
            Sid = Guid.NewGuid().ToString("N"),
            ShopCode = shopCode, OrderId = orderId, Amount = amount, Currency = currency,
            Installment = installment, CardType = cardType, MaskedPan = maskedPan,
            OkUrl = okUrl, FailUrl = failUrl, Rnd = rnd, SecureType = secureType, Scenario = scenario,
            Status = ThreeDSessionStatus.Pending, CreatedAt = now
        };

    /// <summary>ACS tamamlandı: 3D sonucu + (3DModel için) completion'da doğrulanacak MD saklanır.</summary>
    public void Complete(bool threeDPassed, string? md)
    {
        Status = ThreeDSessionStatus.Completed;
        ThreeDPassed = threeDPassed;
        Md = md;
    }
}

public enum ThreeDSessionStatus
{
    Pending = 0,
    Completed = 1
}
