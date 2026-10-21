namespace MaviBank.Api.Domains.Transactions.Features.Commands;

/// <summary>
/// İade (Refund) — yalnız KAPALI batch'teki (Settled) işleme. Ters yönde para hareketi; kısmi olabilir,
/// `RefundedAmount` birikir ve işlem tutarını AŞAMAZ; komisyon geri gelmez (bu dilimde iade komisyonu
/// hesaplanmaz). Açık batch (Authorized) işleme Refund reddedilir (önce gün sonu). Kimlik fail-closed.
/// </summary>
public static class Refund
{
    public record Command(string ShopCode, string UserCode, string UserPass, string OrderId, decimal Amount);

    public record Result(bool Success, string ProcReturnCode, string OrderId, decimal RefundedAmount,
        string? ErrorCode, string? ErrorMessage);

    [Transactional]
    public class RefundHandler
    {
        public async Task<Result> Handle(Command cmd, IDocumentSession session, CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.ShopCode, ct);
            if (merchant is null || merchant.UserCode != cmd.UserCode || merchant.UserPass != cmd.UserPass)
                return new Result(false, InterVposConstants.ProcAuthError, cmd.OrderId, 0m, "AUTH", "Gecersiz uye isyeri bilgileri");

            var streamKey = $"{cmd.ShopCode}:{cmd.OrderId}";
            var tx = await session.Events.AggregateStreamAsync<Transaction>(streamKey, token: ct);
            if (tx is null)
                return new Result(false, InterVposConstants.ProcAuthError, cmd.OrderId, 0m, "NOTFOUND", "Islem bulunamadi");

            if (!tx.CanRefund(cmd.Amount))
                return new Result(false, InterVposConstants.ProcDoNotHonour, cmd.OrderId, tx.RefundedAmount,
                    InterVposConstants.ProcDoNotHonour, "Iade edilemez (gun sonu gormemis ya da tutar asiliyor)");

            var refundedTotal = tx.RefundedAmount + cmd.Amount;
            session.Events.Append(streamKey, new TransactionRefunded(cmd.Amount, refundedTotal, DateTime.UtcNow));

            return new Result(true, InterVposConstants.ProcApproved, cmd.OrderId, refundedTotal, null, null);
        }
    }
}
