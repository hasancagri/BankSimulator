using MaviBank.Api.Options;

namespace MaviBank.Api.Domains.Transactions.Features.Commands;

/// <summary>
/// InterVpos NonSecure Auth (satış). Sıra: para birimi → kimlik (fail-closed, işlem doğmaz) →
/// `(ShopCode,OrderId)` idempotency (mevcut stream → aynı sonuç, çift çekim önleme) → sentinel çekim →
/// onay/issuer-red Transaction olayına yazılır. Kart verisi transient (InvokeAsync-only, saklanmaz —
/// İlke VI); defterde yalnız maskeli PAN.
/// </summary>
public static class Authorize
{
    public record Command(
        string ShopCode, string UserCode, string UserPass, string Pan, string Expiry, string Cvv,
        decimal Amount, int Currency, string OrderId, int Installment, int CardType);

    public record Result(ChargeOutcome Outcome, string OrderId, string? TransId);

    [Transactional]
    public class AuthorizeHandler
    {
        public async Task<Result> Handle(
            Command cmd, IDocumentSession session, ChargeScenarioEngine engine,
            InterVposOptions options, CancellationToken ct)
        {
            // Para birimi — v1 yalnız TL (949). İşlem doğmaz.
            if (cmd.Currency != options.SupportedCurrency)
                return new Result(ChargeOutcome.Error(
                    InterVposConstants.ProcAuthError, "CUR", "Desteklenmeyen para birimi"), cmd.OrderId, null);

            // Kimlik (fail-closed, İlke VII) — eşleşmezse işlem DOĞMAZ.
            var merchant = await session.LoadAsync<Merchant>(cmd.ShopCode, ct);
            if (merchant is null || merchant.UserCode != cmd.UserCode || merchant.UserPass != cmd.UserPass)
                return new Result(ChargeOutcome.Error(
                    InterVposConstants.ProcAuthError, "AUTH", "Gecersiz uye isyeri bilgileri"), cmd.OrderId, null);

            // İdempotency — aynı (ShopCode,OrderId) tekrar: mevcut sonucu döndür, ikinci işlem doğmaz.
            var streamKey = StreamKey(cmd.ShopCode, cmd.OrderId);
            var existing = await session.Events.AggregateStreamAsync<Transaction>(streamKey, token: ct);
            if (existing is not null)
                return Rebuild(existing);

            // Sentinel çekim (gerçek bakiye yok). Kart yoksa engine do-not-honour döner.
            var card = await session.LoadAsync<Card>(cmd.Pan, ct);
            var outcome = engine.Evaluate(card);
            var maskedPan = SemicolonEncoder.MaskPan(cmd.Pan);

            if (outcome.IsApproved)
            {
                var transId = NewTransId();
                session.Events.StartStream<Transaction>(streamKey, new TransactionAuthorized(
                    cmd.ShopCode, cmd.OrderId, transId, outcome.AuthCode!, cmd.Amount, cmd.Currency,
                    cmd.Installment, maskedPan, cmd.CardType, DateTime.UtcNow));
                return new Result(outcome, cmd.OrderId, transId);
            }

            // İssuer reddi → Declined (red bir denemedir, defterde kalır).
            session.Events.StartStream<Transaction>(streamKey, new TransactionDeclined(
                cmd.ShopCode, cmd.OrderId, outcome.ProcReturnCode, outcome.ErrorCode!, outcome.ErrorMessage!,
                cmd.Amount, maskedPan, DateTime.UtcNow));
            return new Result(outcome, cmd.OrderId, null);
        }

        private static string StreamKey(string shopCode, string orderId) => $"{shopCode}:{orderId}";
        private static string NewTransId() => Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

        // İdempotent tekrar: mevcut stream'den sonucu yeniden kur (aynı TransId/kod döner).
        private static Result Rebuild(Transaction tx)
            => tx.Status == TransactionStatus.Authorized
                ? new Result(ChargeOutcome.Approved(tx.AuthCode ?? string.Empty), tx.OrderId, tx.TransId)
                : new Result(ChargeOutcome.Declined(
                    tx.ProcReturnCode ?? InterVposConstants.ProcDoNotHonour,
                    tx.ErrorCode ?? string.Empty, tx.ErrorMessage ?? string.Empty), tx.OrderId, null);
    }
}
