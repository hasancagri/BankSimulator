namespace MaviBank.Api.Domains.Transactions.Features.Commands;

/// <summary>
/// 3D Secure başlat — SecureType=3DModel/3DPay/3DHost. Kimlik + istek Hash doğrulanır (fail-closed, İlke V/
/// VII); geçerliyse bekleyen ThreeDSession saklanır (tam PAN değil, maskeli + senaryo) ve doğrulama kodu
/// "teslim" edilir (ICodeDeliveryChannel). Endpoint bu sonuçtan sahte ACS (MaviSecure) HTML'ini render eder.
/// Hash girdisi merchant'ın gönderdiği ham string değerlerle birebir (ayraçsız).
/// </summary>
public static class ThreeDInitiate
{
    public record Command(
        string ShopCode, string UserCode, string UserPass, string Pan, string Expiry, string Cvv,
        string PurchAmountRaw, string InstallmentRaw, string TxnType, string SecureType, decimal Amount, int Currency,
        string OrderId, int Installment, int CardType, string OkUrl, string FailUrl, string Rnd, string Hash);

    public record Result(bool Valid, string Sid, string OrderId, decimal Amount, string MaskedPan,
        string DeliveredCode, string? ErrorMessage);

    [Transactional]
    public class ThreeDInitiateHandler
    {
        public async Task<Result> Handle(Command cmd, IDocumentSession session, ICodeDeliveryChannel codeChannel,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.ShopCode, ct);
            if (merchant is null || merchant.UserCode != cmd.UserCode || merchant.UserPass != cmd.UserPass)
                return new Result(false, string.Empty, cmd.OrderId, cmd.Amount, string.Empty, string.Empty,
                    "Gecersiz uye isyeri bilgileri");

            // İstek hash doğrulama (ham string değerlerle birebir — İlke V). Rotasyon overlap'i: güncel ya da
            // (24s içindeki) önceki SecretKey ile hesaplanan hash kabul edilir (Faz 7 panel "Yenile").
            var hashOk = merchant.ValidSecretKeys(DateTime.UtcNow).Any(secret =>
                string.Equals(
                    InterVposHash.ComputeRequestHash(cmd.ShopCode, cmd.OrderId, cmd.PurchAmountRaw,
                        cmd.OkUrl, cmd.FailUrl, cmd.TxnType, cmd.InstallmentRaw, cmd.Rnd, secret),
                    cmd.Hash, StringComparison.Ordinal));
            if (!hashOk)
                return new Result(false, string.Empty, cmd.OrderId, cmd.Amount, string.Empty, string.Empty,
                    "Gecersiz hash");

            var card = await session.LoadAsync<Card>(cmd.Pan, ct);
            var scenario = card?.Scenario ?? CardScenario.ThreeDFail; // bilinmeyen kart → 3D geçemez
            var maskedPan = SemicolonEncoder.MaskPan(cmd.Pan);

            var s3d = ThreeDSession.Create(cmd.ShopCode, cmd.OrderId, cmd.Amount, cmd.Currency, cmd.Installment,
                cmd.CardType, maskedPan, cmd.OkUrl, cmd.FailUrl, cmd.Rnd, cmd.SecureType, scenario, DateTime.UtcNow);
            session.Store(s3d);

            var code = codeChannel.Deliver(cmd.OrderId); // demo: ekranda gösterilir (gerçek SMS yok)
            return new Result(true, s3d.Sid, cmd.OrderId, cmd.Amount, maskedPan, code, null);
        }
    }
}
