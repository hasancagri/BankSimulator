namespace Simulator.Core.Domains.Transactions.Features.Queries;

/// <summary>
/// Operasyon yüzü (API Market) — bir batch'in işlem dökümü (mutabakat eşleştirmesi; anahtar = OrderId/TxRef).
/// Gross/Deduction/Net + durum; merchant kendi defteriyle karşılaştırır (adet/tutar uyuşmazlığı = alarm).
/// </summary>
public static class GetBatchDetail
{
    public record Query(string BatchNo);

    public record TxnRow(string OrderId, string? TransId, string TxnStat,
        decimal GrossAmount, decimal DeductionAmount, decimal NetAmount, decimal RefundedAmount);

    public record Result(bool Found, string BatchNo, IReadOnlyList<TxnRow> Transactions);

    public class GetBatchDetailHandler
    {
        public async Task<Result> Handle(Query query, IDocumentSession session, CancellationToken ct)
        {
            var batch = await session.LoadAsync<Batch>(query.BatchNo, ct);
            if (batch is null)
                return new Result(false, query.BatchNo, Array.Empty<TxnRow>());

            var rows = new List<TxnRow>();
            foreach (var key in batch.TransactionKeys)
            {
                var tx = await session.Events.AggregateStreamAsync<Transaction>(key, token: ct);
                if (tx is null) continue;
                rows.Add(new TxnRow(tx.OrderId, tx.TransId, tx.Status.ToString(),
                    tx.Amount, tx.DeductionAmount, tx.NetAmount, tx.RefundedAmount));
            }
            return new Result(true, query.BatchNo, rows);
        }
    }
}
