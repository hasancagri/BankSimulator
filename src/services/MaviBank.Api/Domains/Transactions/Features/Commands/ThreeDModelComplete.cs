namespace MaviBank.Api.Domains.Transactions.Features.Commands;

/// <summary>
/// 3DModel tamamlama — merchant, ACS callback'inden sonra AYRI istek atar (SecureType=NonSecure + MD +
/// OrderId) ve asıl çekim BURADA yapılır (3DPay/3DHost'ta callback nihaiydi; 3DModel'de completion nihai).
/// MD, ACS'te üretilip 3D oturumuna yazılan değerle eşleşmeli (bütünlük). Kimlik fail-closed. Gerçeğe sadık
/// InterVpos 3DModel akışı (gereksinim §1.5 adım 6).
/// </summary>
public static class ThreeDModelComplete
{
    public record Command(string ShopCode, string UserCode, string UserPass, string OrderId, string Md);

    public record Result(bool Success, string ProcReturnCode, string OrderId,
        string? TransId, string? AuthCode, string? ErrorCode, string? ErrorMessage);

    [Transactional]
    public class ThreeDModelCompleteHandler
    {
        public async Task<Result> Handle(Command cmd, IDocumentSession session, ChargeScenarioEngine engine,
            CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.ShopCode, ct);
            if (merchant is null || merchant.UserCode != cmd.UserCode || merchant.UserPass != cmd.UserPass)
                return new Result(false, InterVposConstants.ProcAuthError, cmd.OrderId, null, null, "AUTH", "Gecersiz uye isyeri bilgileri");

            var s3d = await session.Query<ThreeDSession>()
                .Where(x => x.ShopCode == cmd.ShopCode && x.OrderId == cmd.OrderId
                    && x.SecureType == "3DModel" && x.Status == ThreeDSessionStatus.Completed)
                .FirstOrDefaultAsync(ct);

            if (s3d is null)
                return new Result(false, InterVposConstants.ProcAuthError, cmd.OrderId, null, null, "NO3D", "3D oturumu bulunamadi");
            if (!s3d.ThreeDPassed)
                return new Result(false, InterVposConstants.ProcDoNotHonour, cmd.OrderId, null, null, "3DFAIL", "3D dogrulama basarisiz");
            if (!string.Equals(s3d.Md, cmd.Md, StringComparison.Ordinal))
                return new Result(false, InterVposConstants.ProcAuthError, cmd.OrderId, null, null, "MD", "MD uyusmuyor");

            var (proc, _) = await ThreeDCharge.ChargeAsync(s3d, session, engine, DateTime.UtcNow, ct);

            // Çekim sonrası işlem referansları (cevap için).
            var tx = await session.Events.AggregateStreamAsync<Transaction>($"{cmd.ShopCode}:{cmd.OrderId}", token: ct);
            var success = proc == InterVposConstants.ProcApproved;
            return success
                ? new Result(true, proc, cmd.OrderId, tx?.TransId, tx?.AuthCode, null, null)
                : new Result(false, proc, cmd.OrderId, null, null, proc, "Cekim reddedildi");
        }
    }
}
