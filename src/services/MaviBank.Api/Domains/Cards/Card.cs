namespace MaviBank.Api.Domains.Cards;

/// <summary>
/// Test kartı — salt-okur düz Marten dokümanı (seed). Kimlik = `Pan`. `Scenario` sentinel bayrağı çekim
/// sonucunu belirler (İlke VI determinizm; gerçek bakiye/limit YOK).
/// </summary>
public class Card
{
    public string Pan { get; set; } = string.Empty;
    public string Expiry { get; set; } = string.Empty;   // "MMYY"
    public string Cvv { get; set; } = string.Empty;
    public int CardType { get; set; }                     // 0=VISA,1=MC,2=AMEX,3=TROY
    public CardScenario Scenario { get; set; }
}

// Enum aggregate/doküman dosyasında (conventions).
public enum CardScenario
{
    Approve = 0,
    InsufficientFunds = 1,
    Timeout = 2,
    ThreeDFail = 3   // 3D doğrulama başarısız (3DStatus=0); NonSecure'de do-not-honour gibi ele alınır
}
