
namespace Simulator.Core.Domains.Merchants;

/// <summary>
/// Faz 7 üye-işyeri paneli (minimal) — SecretKey yenileme. Gerçekte canlı/test sekmeli panel + maskeli
/// SecretKey + IP whitelist düzenleme; bu dilimde yalnız reissue ucu (sertifikasyon/panel UI seam).
/// </summary>
public static class MerchantPanelEndpointExtension
{
    public static void AddMerchantPanelEndpoints(this WebApplication app)
    {
        app.MapPost("/panel/{shopCode}/reissue-secret", async (string shopCode, IMessageBus bus) =>
            {
                var r = await bus.InvokeAsync<ReissueSecretKey.Result>(new ReissueSecretKey.Command(shopCode));
                return r.Success ? Results.Ok(r) : Results.NotFound(r);
            })
            .WithTags("panel")
            .AllowAnonymous();
    }
}
