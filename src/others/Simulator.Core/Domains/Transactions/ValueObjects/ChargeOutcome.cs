namespace Simulator.Core.Domains.Transactions.ValueObjects;

/// <summary>
/// Normalize çekim sonucu (VO: private ctor + factory). Bank-agnostik — facade (InterVpos/TuruncuBank) tele
/// render eder. Üç şekil: onay / issuer-red (stream'lenir) / kimlik-biçim hatası (stream'lenmez). Red/hata
/// outcome taşır, exception değil (İlke IV). ProcReturnCode = Core ResponseCodes.
/// </summary>
public sealed record ChargeOutcome
{
    public bool IsApproved { get; }
    /// <summary>İşlem defterine yazılmalı mı? Onay + issuer-red = evet; kimlik/biçim hatası = hayır.</summary>
    public bool CreatesTransaction { get; }
    public string ProcReturnCode { get; }
    public string? AuthCode { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private ChargeOutcome(bool isApproved, bool createsTransaction, string procReturnCode,
        string? authCode, string? errorCode, string? errorMessage)
    {
        IsApproved = isApproved;
        CreatesTransaction = createsTransaction;
        ProcReturnCode = procReturnCode;
        AuthCode = authCode;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static ChargeOutcome Approved(string authCode)
        => new(true, true, ResponseCodes.Approved, authCode, null, null);

    public static ChargeOutcome Declined(string procReturnCode, string errorCode, string errorMessage)
        => new(false, true, procReturnCode, null, errorCode, errorMessage);

    public static ChargeOutcome Error(string procReturnCode, string errorCode, string errorMessage)
        => new(false, false, procReturnCode, null, errorCode, errorMessage);
}
