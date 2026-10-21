namespace MaviBank.Api.Domains.Onboarding.Features.Commands;

/// <summary>Admin başvuruyu reddeder (terminal).</summary>
public static class RejectApplication
{
    public record Command(string ApplicationId);

    public record Result(bool Success, string? Status, string? ErrorCode);

    [Transactional]
    public class RejectApplicationHandler
    {
        public async Task<Result> Handle(Command cmd, IDocumentSession session, CancellationToken ct)
        {
            var app = await session.LoadAsync<MerchantApplication>(cmd.ApplicationId, ct);
            if (app is null) return new Result(false, null, OnboardingConstants.APPLICATION_NOT_FOUND);

            var r = app.Reject();
            if (!r.IsSuccess) return new Result(false, app.Status.ToString(), r.Messages?[0].Code);

            session.Store(app);
            return new Result(true, app.Status.ToString(), null);
        }
    }
}
