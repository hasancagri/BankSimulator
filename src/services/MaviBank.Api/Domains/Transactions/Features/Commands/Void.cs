namespace MaviBank.Api.Domains.Transactions.Features.Commands;

/// <summary>
/// İptal (Void) — yalnız AÇIK batch'teki (Authorized) işleme. Para hareketi yoktu → iz bırakmadan ters
/// çevrilir (TransactionVoided), açık batch'ten çıkarılır, komisyon doğmaz. Settled (gün sonu görmüş) işleme
/// Void reddedilir (iade gerekir). Kimlik fail-closed (İlke VII).
/// </summary>
public static class VoidTransaction
{
    public record Command(string ShopCode, string UserCode, string UserPass, string OrderId);

    public record Result(bool Success, string ProcReturnCode, string OrderId, string? ErrorCode, string? ErrorMessage);

    [Transactional]
    public class VoidHandler
    {
        public async Task<Result> Handle(Command cmd, IDocumentSession session, CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.ShopCode, ct);
            if (merchant is null || merchant.UserCode != cmd.UserCode || merchant.UserPass != cmd.UserPass)
                return new Result(false, InterVposConstants.ProcAuthError, cmd.OrderId, "AUTH", "Gecersiz uye isyeri bilgileri");

            var streamKey = $"{cmd.ShopCode}:{cmd.OrderId}";
            var tx = await session.Events.AggregateStreamAsync<Transaction>(streamKey, token: ct);
            if (tx is null)
                return new Result(false, InterVposConstants.ProcAuthError, cmd.OrderId, "NOTFOUND", "Islem bulunamadi");

            if (!tx.CanVoid())
                return new Result(false, InterVposConstants.ProcDoNotHonour, cmd.OrderId, InterVposConstants.ProcDoNotHonour, "Gun sonu gecti, iptal edilemez");

            session.Events.Append(streamKey, new TransactionVoided(DateTime.UtcNow));

            // Açık batch'ten çıkar (iz bırakmaz; settlement görmesin).
            var batch = await session.Query<Batch>()
                .Where(b => b.ShopCode == cmd.ShopCode && b.Status == BatchStatus.Open)
                .FirstOrDefaultAsync(ct);
            if (batch is not null)
            {
                batch.RemoveTransaction(streamKey);
                session.Store(batch);
            }

            return new Result(true, InterVposConstants.ProcApproved, cmd.OrderId, null, null);
        }
    }
}
