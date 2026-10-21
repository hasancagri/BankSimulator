using MaviBank.Api.Domains.Onboarding.Features.Commands;

namespace MaviBank.Api.Domains.Onboarding;

/// <summary>
/// Faz 7 — onboarding yüzü (başvuru + admin değerlendirme + aktivasyon). Gerçekte portal + admin ekranı
/// (ileride MCP/AgentPlatform); sim dev HTTP uçları. Aktivasyon credential reveal eder (bir kez).
/// </summary>
public static class OnboardingEndpointExtension
{
    public record ApplyBody(string CompanyTitle, string TaxNo, string Website, string Sector,
        string Iban, string ContactEmail, decimal EstimatedTurnover);

    public static void AddOnboardingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/onboarding").WithTags("onboarding").AllowAnonymous();

        group.MapPost("/apply", async (ApplyBody b, IMessageBus bus) =>
        {
            var r = await bus.InvokeAsync<SubmitApplication.Result>(new SubmitApplication.Command(
                b.CompanyTitle, b.TaxNo, b.Website, b.Sector, b.Iban, b.ContactEmail, b.EstimatedTurnover));
            return r.Success ? Results.Ok(r) : Results.BadRequest(r);
        });

        group.MapPost("/{id}/advance", async (string id, IMessageBus bus) =>
        {
            var r = await bus.InvokeAsync<AdvanceApplication.Result>(new AdvanceApplication.Command(id));
            return r.Success ? Results.Ok(r) : Results.BadRequest(r);
        });

        group.MapPost("/{id}/reject", async (string id, IMessageBus bus) =>
        {
            var r = await bus.InvokeAsync<RejectApplication.Result>(new RejectApplication.Command(id));
            return r.Success ? Results.Ok(r) : Results.BadRequest(r);
        });

        // Aktivasyon → credential reveal (bir kez; gerçekte portal ekranı).
        group.MapPost("/{id}/activate", async (string id, IMessageBus bus) =>
        {
            var r = await bus.InvokeAsync<ActivateApplication.Result>(new ActivateApplication.Command(id));
            return r.Success ? Results.Ok(r) : Results.BadRequest(r);
        });
    }
}
