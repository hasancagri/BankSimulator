namespace Simulator.Core.Domains.Accounts.Features.Queries;

/// <summary>
/// Faz 6 operasyon — POS hesap durumu + hareket dökümü (GetPosAccountDetail / GetAccountTransactionList).
/// Kullanılabilir + blokeli bakiye + ekstre (settlement pending / POS alacağı). Pull modeli.
/// </summary>
public static class GetAccountDetail
{
    public record Query(string ShopCode);

    public record MovementRow(DateTime At, string Type, decimal Amount, string OrderId, string Note);

    public record Result(bool Found, string ShopCode, decimal AvailableBalance, decimal PendingBalance,
        IReadOnlyList<MovementRow> Movements);

    public class GetAccountDetailHandler
    {
        public async Task<Result> Handle(Query query, IDocumentSession session, CancellationToken ct)
        {
            var account = await session.LoadAsync<Account>(query.ShopCode, ct);
            if (account is null)
                return new Result(false, query.ShopCode, 0m, 0m, Array.Empty<MovementRow>());

            var rows = account.Movements
                .Select(m => new MovementRow(m.At, m.Type, m.Amount, m.OrderId, m.Note))
                .ToList();
            return new Result(true, query.ShopCode, account.AvailableBalance, account.PendingBalance, rows);
        }
    }
}
