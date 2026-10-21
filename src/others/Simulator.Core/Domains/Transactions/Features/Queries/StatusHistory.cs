namespace Simulator.Core.Domains.Transactions.Features.Queries;

/// <summary>
/// Durum sorgu (StatusHistory) — bir OrderId'nin güncel halini döner (PG in-doubt / çift-çekim emniyeti,
/// Ders 16). Salt-okur (event stream'den türetir). Kimlik doğrulaması Auth kadar sıkı değil (sorgu);
/// (ShopCode,OrderId) yoksa bulunamadı. Kart verisi/sır DÖNMEZ.
/// </summary>
public static class StatusHistory
{
    public record Query(string ShopCode, string OrderId);

    public record Result(bool Found, string ProcReturnCode, string OrderId, string TxnStat,
        DateTime? VoidDate, decimal RefundedAmount, string? ErrorCode, string? ErrorMessage);

    public class StatusHistoryHandler
    {
        public async Task<Result> Handle(Query query, IDocumentSession session, CancellationToken ct)
        {
            var streamKey = $"{query.ShopCode}:{query.OrderId}";
            var tx = await session.Events.AggregateStreamAsync<Transaction>(streamKey, token: ct);
            if (tx is null)
                return new Result(false, ResponseCodes.AuthError, query.OrderId, string.Empty,
                    null, 0m, "NOTFOUND", "Islem bulunamadi");

            return new Result(true, ResponseCodes.Approved, query.OrderId,
                tx.Status.ToString(), tx.VoidDate, tx.RefundedAmount, null, null);
        }
    }
}
