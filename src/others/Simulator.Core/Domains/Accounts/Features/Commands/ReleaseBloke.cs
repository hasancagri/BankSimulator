namespace Simulator.Core.Domains.Accounts.Features.Commands;

/// <summary>
/// Faz 6 — valörü dolan blokeli tutarları kullanılabilire geçirir (manuel/dev tetik; gerçekte zamanlanmış
/// Process). "POS alacağı hesaba düştü" hareketi doğurur. Zamanlanmış release (Process/timer) seam.
/// </summary>
public static class ReleaseBloke
{
    public record Command(string ShopCode);

    public record Result(bool Found, string ShopCode, int ReleasedCount, decimal AvailableBalance, decimal PendingBalance);

    [Transactional]
    public class ReleaseBlokeHandler
    {
        public async Task<Result> Handle(Command cmd, IDocumentSession session, CancellationToken ct)
        {
            var account = await session.LoadAsync<Account>(cmd.ShopCode, ct);
            if (account is null)
                return new Result(false, cmd.ShopCode, 0, 0m, 0m);

            var count = account.ReleaseMatured(DateTime.UtcNow);
            session.Store(account);
            return new Result(true, cmd.ShopCode, count, account.AvailableBalance, account.PendingBalance);
        }
    }
}
