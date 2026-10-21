using MaviBank.Api.Domains.Transactions.Features.Commands;

namespace MaviBank.Api.Ops;

/// <summary>
/// Dev manuel gün-sonu tetiği — `POST /ops/end-of-day` (JSON {shopCode}). Açık batch'i kapatır/settle eder.
/// Operasyon yüzü (API Market JSON zarf + SubscriptionKey + rapor uçları + MCP) Faz 3'te formalize edilir;
/// bu yalnız determinist test/demo için minimal tetik. Zamanlanmış kesim (Process/timer) seam.
/// </summary>
public static class EndOfDayEndpointExtension
{
    public record EndOfDayBody(string ShopCode);

    public static void AddOpsEndpoints(this WebApplication app)
    {
        app.MapPost("/ops/end-of-day", async (EndOfDayBody body, IMessageBus bus) =>
            {
                var summary = await bus.InvokeAsync<EndOfDay.Summary>(new EndOfDay.Command(body.ShopCode));
                return summary.Found ? Results.Ok(summary) : Results.NotFound(summary);
            })
            .WithTags("ops")
            .AllowAnonymous();
    }
}
