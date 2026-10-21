namespace Simulator.Core;

// Nested *Handler'lar ad-son-eki taramasıyla bulunmaz (CLAUDE.md tuzağı) → host Wolverine'de tek tek
// IncludeType. Merkezi helper: her host `opts.IncludeCoreHandlers()` çağırır (banka başına tekrar yok).
public static class WolverineRegistration
{
    public static void IncludeCoreHandlers(this WolverineOptions opts)
    {
        opts.Discovery.IncludeType(typeof(Authorize.AuthorizeHandler));
        opts.Discovery.IncludeType(typeof(VoidTransaction.VoidHandler));
        opts.Discovery.IncludeType(typeof(Refund.RefundHandler));
        opts.Discovery.IncludeType(typeof(EndOfDay.EndOfDayHandler));
        opts.Discovery.IncludeType(typeof(StatusHistory.StatusHistoryHandler));
        opts.Discovery.IncludeType(typeof(GetBatchSummary.GetBatchSummaryHandler));
        opts.Discovery.IncludeType(typeof(GetBatchDetail.GetBatchDetailHandler));
        opts.Discovery.IncludeType(typeof(GetTransactionList.GetTransactionListHandler));
        opts.Discovery.IncludeType(typeof(ReleaseBloke.ReleaseBlokeHandler));
        opts.Discovery.IncludeType(typeof(GetAccountDetail.GetAccountDetailHandler));
        opts.Discovery.IncludeType(typeof(ReissueSecretKey.ReissueSecretKeyHandler));
        opts.Discovery.IncludeType(typeof(SubmitApplication.SubmitApplicationHandler));
        opts.Discovery.IncludeType(typeof(AdvanceApplication.AdvanceApplicationHandler));
        opts.Discovery.IncludeType(typeof(RejectApplication.RejectApplicationHandler));
        opts.Discovery.IncludeType(typeof(ActivateApplication.ActivateApplicationHandler));
    }
}
