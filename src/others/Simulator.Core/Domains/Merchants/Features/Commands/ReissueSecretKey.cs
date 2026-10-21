namespace Simulator.Core.Domains.Merchants.Features.Commands;

/// <summary>
/// Faz 7 panel — SecretKey yenileme ("Göster/Yenile"). Yeni TEST_ anahtar üretilir; eski anahtar 24 saat
/// daha geçerli kalır (3D hash overlap — merchant entegrasyonu kesilmeden geçiş). Yeni anahtar bir kez
/// reveal edilir (sır; log'a/sonraki DTO'ya girmez).
/// </summary>
public static class ReissueSecretKey
{
    private static readonly TimeSpan Overlap = TimeSpan.FromHours(24);

    public record Command(string ShopCode);

    public record Result(bool Success, string? NewSecretKey, string? ErrorCode);

    [Transactional]
    public class ReissueSecretKeyHandler
    {
        public async Task<Result> Handle(Command cmd, IDocumentSession session, CancellationToken ct)
        {
            var merchant = await session.LoadAsync<Merchant>(cmd.ShopCode, ct);
            if (merchant is null)
                return new Result(false, null, "NOT_FOUND");

            var newKey = "TEST_" + Guid.NewGuid().ToString("N");
            merchant.ReissueSecretKey(newKey, DateTime.UtcNow, Overlap);
            session.Store(merchant);
            return new Result(true, newKey, null);
        }
    }
}
