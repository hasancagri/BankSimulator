namespace Simulator.Core.Domains.Onboarding.Features.Commands;

/// <summary>
/// Teknik aktivasyon — yalnız sözleşme imzalanmışsa. Test credential üretilir (ShopCode/UserCode/UserPass/
/// SecretKey=TEST_*/SubscriptionKey), Merchant doğar (ödeme yüzü artık bu merchant'ı tanır) ve credential
/// BİR KEZ reveal edilir (gerçekte portal/ekran; sim yanıtta döner — İlke VI: kontrol düzlemi normalde sır
/// döndürmez, reveal tek-kullanım insan-handoff istisnası). Sertifikasyon kapısı seam (sonraki).
/// </summary>
public static class ActivateApplication
{
    public record Command(string ApplicationId);

    public record Result(bool Success, string? ErrorCode,
        string? ShopCode, string? UserCode, string? UserPass, string? SecretKey, string? SubscriptionKey);

    [Transactional]
    public class ActivateApplicationHandler
    {
        public async Task<Result> Handle(Command cmd, IDocumentSession session, CancellationToken ct)
        {
            var app = await session.LoadAsync<MerchantApplication>(cmd.ApplicationId, ct);
            if (app is null)
                return new Result(false, OnboardingConstants.APPLICATION_NOT_FOUND, null, null, null, null, null);

            // Teknik credential üret (test ortamı — TEST_* prefix).
            var shopCode = "MAVI" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            var userCode = "API" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant();
            var userPass = Guid.NewGuid().ToString("N")[..10];
            var secretKey = "TEST_" + Guid.NewGuid().ToString("N");
            var subKey = "TEST_SUB_" + Guid.NewGuid().ToString("N")[..8];

            var r = app.Activate(shopCode);
            if (!r.IsSuccess)
                return new Result(false, r.Messages?[0].Code, null, null, null, null, null);

            // Ödeme yüzünün tanıyacağı Merchant (varsayılan komisyon/bloke).
            session.Store(new Merchant
            {
                ShopCode = shopCode, UserCode = userCode, UserPass = userPass,
                SecretKey = secretKey, SubscriptionKey = subKey,
                CommissionRates = new Dictionary<int, decimal> { { 1, 0.0179m }, { 3, 0.0219m }, { 6, 0.0269m } },
                DefaultCommissionRate = 0.0179m, BlokeGun = 1
            });
            session.Store(app);

            // Reveal (bir kez; gerçekte portal ekranı). SecretKey yalnız burada döner — log'a/sonra DTO'ya girmez.
            return new Result(true, null, shopCode, userCode, userPass, secretKey, subKey);
        }
    }
}
