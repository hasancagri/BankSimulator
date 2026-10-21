using MaviBank.Api.Domains.Transactions.Features.Queries;
using MaviBank.Api.Domains.Accounts.Features.Queries;

namespace MaviBank.Api.Ops.ApiMarket;

/// <summary>
/// Faz 3 — API Market POS operasyon yüzü (mutabakat okuma). `POST /api/*` JSON zarf (Header+Parameters →
/// {Data}); kimlik `SubscriptionKey` header'ı (seed'li merchant'a çözülür, fail-closed). Ödeme yüzünden
/// (InterVpos `;;`) AYRI yüz. Bu dilim mutabakat çifti: GetPosBatchSummary + GetPosBatchDetail (pull modeli —
/// banka haber vermez, merchant sorar). Diğer uçlar + MCP (AgentPlatform gateway mount) sonraki.
/// </summary>
public static class OperationEndpointExtension
{
    private const string SubscriptionHeader = "SubscriptionKey";

    public static void AddOperationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").WithTags("operation").AllowAnonymous();

        group.MapPost("/GetPosBatchSummary", async (ApiRequest body, HttpRequest req, IMessageBus bus,
            IDocumentSession session, CancellationToken ct) =>
        {
            var merchant = await Authenticate(req, session, ct);
            if (merchant is null) return Results.Unauthorized();

            var shopCode = body.Get("ShopCode") ?? merchant.ShopCode;
            var r = await bus.InvokeAsync<GetBatchSummary.Result>(new GetBatchSummary.Query(shopCode));
            return Results.Ok(ApiResponse.Of(r));
        });

        group.MapPost("/GetPosBatchDetail", async (ApiRequest body, HttpRequest req, IMessageBus bus,
            IDocumentSession session, CancellationToken ct) =>
        {
            var merchant = await Authenticate(req, session, ct);
            if (merchant is null) return Results.Unauthorized();

            var batchNo = body.Get("BatchNo") ?? string.Empty;
            var r = await bus.InvokeAsync<GetBatchDetail.Result>(new GetBatchDetail.Query(batchNo));
            return r.Found ? Results.Ok(ApiResponse.Of(r)) : Results.NotFound(ApiResponse.Of(r));
        });

        // Faz 6: POS hesap durumu (kullanılabilir/blokeli bakiye) + hesap hareket dökümü (ekstre).
        group.MapPost("/GetPosAccountDetail", async (ApiRequest body, HttpRequest req, IMessageBus bus,
            IDocumentSession session, CancellationToken ct) =>
        {
            var merchant = await Authenticate(req, session, ct);
            if (merchant is null) return Results.Unauthorized();
            var shopCode = body.Get("ShopCode") ?? merchant.ShopCode;
            var r = await bus.InvokeAsync<GetAccountDetail.Result>(new GetAccountDetail.Query(shopCode));
            return Results.Ok(ApiResponse.Of(new { r.ShopCode, r.AvailableBalance, r.PendingBalance }));
        });

        group.MapPost("/GetAccountTransactionList", async (ApiRequest body, HttpRequest req, IMessageBus bus,
            IDocumentSession session, CancellationToken ct) =>
        {
            var merchant = await Authenticate(req, session, ct);
            if (merchant is null) return Results.Unauthorized();
            var shopCode = body.Get("ShopCode") ?? merchant.ShopCode;
            var r = await bus.InvokeAsync<GetAccountDetail.Result>(new GetAccountDetail.Query(shopCode));
            return Results.Ok(ApiResponse.Of(new { r.ShopCode, r.Movements }));
        });
    }

    // SubscriptionKey → seed'li merchant (fail-closed). Sır header'da; cevaba/log'a girmez (İlke VI).
    private static async Task<Merchant?> Authenticate(HttpRequest req, IDocumentSession session, CancellationToken ct)
    {
        var key = req.Headers[SubscriptionHeader].ToString();
        if (string.IsNullOrEmpty(key)) return null;
        return await session.Query<Merchant>().Where(m => m.SubscriptionKey == key).FirstOrDefaultAsync(ct);
    }
}
