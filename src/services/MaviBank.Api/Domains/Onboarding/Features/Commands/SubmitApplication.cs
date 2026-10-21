namespace MaviBank.Api.Domains.Onboarding.Features.Commands;

/// <summary>Üye-işyeri başvurusu alınır (portal). Teknik alan yok — firma/ciro/iletişim/IBAN (Received).</summary>
public static class SubmitApplication
{
    public record Command(string CompanyTitle, string TaxNo, string Website, string Sector,
        string Iban, string ContactEmail, decimal EstimatedTurnover);

    public record Result(bool Success, string? ApplicationId, string? Status, string? ErrorCode);

    [Transactional]
    public class SubmitApplicationHandler
    {
        public async Task<Result> Handle(Command cmd, IDocumentSession session, CancellationToken ct)
        {
            var app = MerchantApplication.Submit(cmd.CompanyTitle, cmd.TaxNo, cmd.Website, cmd.Sector,
                cmd.Iban, cmd.ContactEmail, cmd.EstimatedTurnover);
            if (!app.IsSuccess)
                return new Result(false, null, null, app.Messages?[0].Code);

            session.Store(app.Data!);
            return new Result(true, app.Data!.Id, app.Data!.Status.ToString(), null);
        }
    }
}
