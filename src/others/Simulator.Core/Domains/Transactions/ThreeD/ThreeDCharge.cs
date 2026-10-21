namespace Simulator.Core.Domains.Transactions.ThreeD;

/// <summary>
/// 3D doğrulandıktan sonra asıl çekim (Transaction + batch). 3DPay/3DHost'ta ACS callback'inde, 3DModel'de
/// ayrı completion adımında çağrılır — paylaşılan tek kopya (üçleme yerine; Authorize ile aynı desen,
/// bilinçli ayrı çünkü girdi 3D oturumu). Senaryo kararı (gerçek bakiye yok). İdempotent (mevcut stream).
/// </summary>
public static class ThreeDCharge
{
    public static async Task<(string ProcReturnCode, string TxnStat)> ChargeAsync(
        ThreeDSession s3d, IDocumentSession session, ChargeScenarioEngine engine, DateTime now, CancellationToken ct)
    {
        var streamKey = $"{s3d.ShopCode}:{s3d.OrderId}";
        var existing = await session.Events.AggregateStreamAsync<Transaction>(streamKey, token: ct);
        if (existing is not null)
            return (existing.ProcReturnCode ?? ResponseCodes.Approved, existing.Status.ToString());

        var outcome = engine.Evaluate(new Card { Scenario = s3d.Scenario });
        if (outcome.IsApproved)
        {
            var batch = await session.Query<Batch>()
                .Where(b => b.ShopCode == s3d.ShopCode && b.Status == BatchStatus.Open)
                .FirstOrDefaultAsync(ct) ?? Batch.OpenFor(s3d.ShopCode, now);
            batch.AddTransaction(streamKey);
            session.Store(batch);

            session.Events.StartStream<Transaction>(streamKey, new TransactionAuthorized(
                s3d.ShopCode, s3d.OrderId, batch.BatchNo, Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
                outcome.AuthCode!, s3d.Amount, s3d.Currency, s3d.Installment, s3d.MaskedPan, s3d.CardType, now));
            return (ResponseCodes.Approved, nameof(TransactionStatus.Authorized));
        }

        session.Events.StartStream<Transaction>(streamKey, new TransactionDeclined(
            s3d.ShopCode, s3d.OrderId, outcome.ProcReturnCode, outcome.ErrorCode ?? string.Empty,
            outcome.ErrorMessage ?? string.Empty, s3d.Amount, s3d.MaskedPan, now));
        return (outcome.ProcReturnCode, nameof(TransactionStatus.Declined));
    }
}
