namespace MaviBank.Api.Domains.Transactions.Features.Queries;

/// <summary>
/// Operasyon yüzü (API Market) — kapalı batch özetleri (merchant mutabakat job'ı için; pull modeli).
/// Yalnız KAPALI batch'ler (gün sonu görmüş) döner — gerçek API kuralı (açık batch raporlanmaz).
/// </summary>
public static class GetBatchSummary
{
    public record Query(string ShopCode);

    public record BatchRow(string BatchNo, DateTime? EndOfDayDate, int TransactionCount,
        decimal GrossTotal, decimal DeductionTotal, decimal NetTotal);

    public record Result(string ShopCode, IReadOnlyList<BatchRow> Batches);

    public class GetBatchSummaryHandler
    {
        public async Task<Result> Handle(Query query, IDocumentSession session, CancellationToken ct)
        {
            var batches = await session.Query<Batch>()
                .Where(b => b.ShopCode == query.ShopCode && b.Status == BatchStatus.Closed)
                .ToListAsync(ct);

            var rows = batches
                .Select(b => new BatchRow(b.BatchNo, b.EndOfDayDate, b.TransactionKeys.Count,
                    b.GrossTotal, b.DeductionTotal, b.NetTotal))
                .ToList();
            return new Result(query.ShopCode, rows);
        }
    }
}
