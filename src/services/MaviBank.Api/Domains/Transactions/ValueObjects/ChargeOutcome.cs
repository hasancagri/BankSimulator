using MaviBank.Api.Domains.Transactions.InterVpos;

namespace MaviBank.Api.Domains.Transactions.ValueObjects;

/// <summary>
/// Normalize çekim sonucu (VO: private ctor + factory). Üç şekil: onay / issuer-red (stream'lenir) /
/// kimlik-biçim hatası (stream'lenmez, handler ön-kontrolü). Red/hata outcome taşır — exception değil (İlke IV).
/// </summary>
public sealed record ChargeOutcome
{
    public bool IsApproved { get; }
    /// <summary>İşlem defterine yazılmalı mı? Onay + issuer-red = evet; kimlik/biçim hatası = hayır (işlem doğmaz).</summary>
    public bool CreatesTransaction { get; }
    public string ProcReturnCode { get; }
    public string Response { get; }
    public string? AuthCode { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private ChargeOutcome(bool isApproved, bool createsTransaction, string procReturnCode, string response,
        string? authCode, string? errorCode, string? errorMessage)
    {
        IsApproved = isApproved;
        CreatesTransaction = createsTransaction;
        ProcReturnCode = procReturnCode;
        Response = response;
        AuthCode = authCode;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>Onay — işlem defterine Authorized yazılır.</summary>
    public static ChargeOutcome Approved(string authCode)
        => new(true, true, InterVposConstants.ProcApproved, InterVposConstants.ResponseApproved, authCode, null, null);

    /// <summary>İssuer reddi — işlem defterine Declined yazılır (red bir denemedir).</summary>
    public static ChargeOutcome Declined(string procReturnCode, string errorCode, string errorMessage)
        => new(false, true, procReturnCode, InterVposConstants.ResponseDeclined, null, errorCode, errorMessage);

    /// <summary>Kimlik/biçim hatası — işlem DOĞMAZ (fail-closed ön-kontrol).</summary>
    public static ChargeOutcome Error(string procReturnCode, string errorCode, string errorMessage)
        => new(false, false, procReturnCode, InterVposConstants.ResponseError, null, errorCode, errorMessage);
}
