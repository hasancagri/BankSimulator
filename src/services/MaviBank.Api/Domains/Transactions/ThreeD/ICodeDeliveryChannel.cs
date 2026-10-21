namespace MaviBank.Api.Domains.Transactions.ThreeD;

/// <summary>
/// İlke VI: 3DS doğrulama kodu teslim kanalı — gerçek SMS YOK (dış bağımlılık/determinizm bozar; iyzico
/// dersi). Gerçek ACS de SMS'i kendi atmaz, kanal altyapısına delege eder — bu arayüz o soyutlama.
/// Varsayılan sabit kod (test). İleride OnScreen/Log/Netgsm takılabilir; varsayılan asla dış servis.
/// </summary>
public interface ICodeDeliveryChannel
{
    /// <summary>Kodu "teslim eder" (demo: ekranda göstermek için döner). Test determinizmi için sabit.</summary>
    string Deliver(string orderId);

    /// <summary>Girilen kodu doğrular.</summary>
    bool Verify(string code);
}

// Varsayılan test kanalı — 123456 her zaman geçer (determinist).
public class FixedCodeChannel : ICodeDeliveryChannel, ISingletonDependency
{
    public const string FixedCode = "123456";
    public string Deliver(string orderId) => FixedCode;
    public bool Verify(string code) => code == FixedCode;
}
