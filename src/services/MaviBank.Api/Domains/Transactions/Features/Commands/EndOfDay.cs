namespace MaviBank.Api.Domains.Transactions.Features.Commands;

/// <summary>
/// Gün sonu (settlement) — manuel tetik. Merchant'ın açık batch'ini kapatır: her onaylı (Authorized) işlem
/// için komisyon (taksit oranı) düşülür, net + bloke/valör hesaplanır (`TransactionSettled`), batch Closed +
/// toplamlar mühürlenir, yeni açık batch açılır. Void edilmiş işlemler batch'ten zaten çıkmıştır. Zamanlanmış
/// kesim (Process/timer) + operasyon API raporları sonraki faz.
/// </summary>
public static class EndOfDay
{
    public record Command(string ShopCode);

    public record Summary(bool Found, string BatchNo, int SettledCount,
        decimal GrossTotal, decimal DeductionTotal, decimal NetTotal);

    [Transactional]
    public class EndOfDayHandler
    {
        public async Task<Summary> Handle(Command cmd, IDocumentSession session, CancellationToken ct)
        {
            var batch = await session.Query<Batch>()
                .Where(b => b.ShopCode == cmd.ShopCode && b.Status == BatchStatus.Open)
                .FirstOrDefaultAsync(ct);
            if (batch is null)
                return new Summary(false, string.Empty, 0, 0m, 0m, 0m);

            var merchant = await session.LoadAsync<Merchant>(cmd.ShopCode, ct);
            var now = DateTime.UtcNow;
            decimal gross = 0m, deductionTotal = 0m, netTotal = 0m;
            var count = 0;

            foreach (var key in batch.TransactionKeys)
            {
                var tx = await session.Events.AggregateStreamAsync<Transaction>(key, token: ct);
                if (tx is null || tx.Status != TransactionStatus.Authorized) continue;

                var rate = merchant?.RateFor(tx.Installment) ?? 0m;
                var deduction = Math.Round(tx.Amount * rate, 2, MidpointRounding.AwayFromZero);
                var net = tx.Amount - deduction;
                var bloke = now.Date.AddDays(merchant?.BlokeGun ?? 0);

                session.Events.Append(key, new TransactionSettled(batch.BatchNo, deduction, net, bloke, now));

                gross += tx.Amount;
                deductionTotal += deduction;
                netTotal += net;
                count++;
            }

            batch.Close(gross, deductionTotal, netTotal, now);
            session.Store(batch);

            // Yeni açık batch (sonraki işlemler buraya).
            session.Store(Batch.OpenFor(cmd.ShopCode, now));

            return new Summary(true, batch.BatchNo, count, gross, deductionTotal, netTotal);
        }
    }
}
