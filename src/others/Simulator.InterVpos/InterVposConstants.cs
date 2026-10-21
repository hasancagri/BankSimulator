namespace Simulator.InterVpos;

// InterVpos protokol sabitleri (yalnız tel string'leri; ProcReturnCode değerleri Core ResponseCodes'ta).
public static class InterVposConstants
{
    public const string TxnTypeAuth = "Auth";
    public const string TxnTypeVoid = "Void";
    public const string TxnTypeRefund = "Refund";
    public const string TxnTypeStatusHistory = "StatusHistory";
    public const string SecureTypeNonSecure = "NonSecure";
    public const string ResponseApproved = "Approved";
    public const string ResponseDeclined = "Declined";
    public const string ResponseError = "Error";
}
