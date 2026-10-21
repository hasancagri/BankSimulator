namespace Simulator.Core.Domains.Transactions;

/// <summary>
/// Sentinel test-kartı → normalize çekim sonucu (İlke VI determinizm; gerçek bakiye YOK). Saf domain-service
/// (dış çağrı yok → domain-TDD). Bilinmeyen kart → onaylanmaz (do-not-honour). AuthCode burada üretilir
/// (benzersiz; idempotent tekrar mevcut stream'den döner, bu yüzden yeni üretim yalnız yeni işlemde olur).
/// </summary>
public class ChargeScenarioEngine : IScopedDependency
{
    /// <summary>Kart senaryosuna göre çekim sonucunu belirler. Onay/İssuer-red stream'lenir; kart yoksa red.</summary>
    public ChargeOutcome Evaluate(Card? card)
    {
        if (card is null)
            return ChargeOutcome.Declined(ResponseCodes.DoNotHonour, ResponseCodes.DoNotHonour, "Islem onaylanmadi");

        return card.Scenario switch
        {
            CardScenario.Approve => ChargeOutcome.Approved(NewAuthCode()),
            CardScenario.InsufficientFunds => ChargeOutcome.Declined(
                ResponseCodes.InsufficientFunds, ResponseCodes.InsufficientFunds, "Yetersiz bakiye"),
            CardScenario.Timeout => ChargeOutcome.Declined(
                ResponseCodes.Timeout, ResponseCodes.Timeout, "Issuer zaman asimi"),
            CardScenario.Fraud => ChargeOutcome.Declined(
                ResponseCodes.Fraud, ResponseCodes.Fraud, "Supheli islem"), // retry EDİLMEZ (fraud)
            _ => ChargeOutcome.Declined(ResponseCodes.DoNotHonour, ResponseCodes.DoNotHonour, "Islem onaylanmadi")
        };
    }

    // Banka onay kodu (6 hane). Benzersiz; determinizm idempotency ile sağlanır (tekrar = mevcut stream).
    private static string NewAuthCode() => "P" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
}
