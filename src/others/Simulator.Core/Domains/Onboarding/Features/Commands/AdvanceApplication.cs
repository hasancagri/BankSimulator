namespace Simulator.Core.Domains.Onboarding.Features.Commands;

/// <summary>Admin değerlendirmeyi bir sonraki adıma ilerletir (Risk→Site→Teklif→Sözleşme).</summary>
public static class AdvanceApplication
{
    public record Command(string ApplicationId);

    public record Result(bool Success, string? Status, string? ErrorCode);

    [Transactional]
    public class AdvanceApplicationHandler
    {
        public async Task<Result> Handle(Command cmd, IDocumentSession session, CancellationToken ct)
        {
            var app = await session.LoadAsync<MerchantApplication>(cmd.ApplicationId, ct);
            if (app is null) return new Result(false, null, OnboardingConstants.APPLICATION_NOT_FOUND);

            var r = app.Advance();
            if (!r.IsSuccess) return new Result(false, app.Status.ToString(), r.Messages?[0].Code);

            session.Store(app);
            return new Result(true, app.Status.ToString(), null);
        }
    }
}
