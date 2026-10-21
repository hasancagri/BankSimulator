namespace Simulator.Core.Domains.Transactions.Features.Queries;

/// <summary>
/// Operasyon yüzü — merchant'ın KAPALI batch'lerindeki tüm işlem dökümü (GetPosTransactionList; gerçek API
/// kuralı: yalnız gün-sonu görmüş işlemler listelenir). Mutabakat/denetim için.
/// </summary>
public static class GetTransactionList
{
    public record Query(string ShopCode);

    public record TxnRow(string OrderId, string? TransId, string TxnStat, string BatchNo,
        decimal GrossAmount, decimal NetAmount, decimal RefundedAmount);

    public record Result(string ShopCode, IReadOnlyList<TxnRow> Transactions);

    public class GetTransactionListHandler
    {
        public async Task<Result> Handle(Query query, IDocumentSession session, CancellationToken ct)
        {
            var batches = await session.Query<Batch>()
                .Where(b => b.ShopCode == query.ShopCode && b.Status == BatchStatus.Closed)
                .ToListAsync(ct);

            var rows = new List<TxnRow>();
            foreach (var b in batches)
                foreach (var key in b.TransactionKeys)
                {
                    var tx = await session.Events.AggregateStreamAsync<Transaction>(key, token: ct);
                    if (tx is null) continue;
                    rows.Add(new TxnRow(tx.OrderId, tx.TransId, tx.Status.ToString(), b.BatchNo,
                        tx.Amount, tx.NetAmount, tx.RefundedAmount));
                }
            return new Result(query.ShopCode, rows);
        }
    }
}
