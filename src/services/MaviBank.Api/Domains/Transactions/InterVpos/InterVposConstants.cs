namespace MaviBank.Api.Domains.Transactions.InterVpos;

// InterVpos protokol sabitleri (tel değerleri — İlke V birebir). ProcReturnCode="00" onay, diğer her şey red.
public static class InterVposConstants
{
    public const string TxnTypeAuth = "Auth";
    public const string TxnTypeVoid = "Void";
    public const string TxnTypeRefund = "Refund";
    public const string TxnTypeStatusHistory = "StatusHistory";
    public const string SecureTypeNonSecure = "NonSecure";

    // ProcReturnCode değerleri
    public const string ProcApproved = "00";            // onay
    public const string ProcInsufficientFunds = "51";   // limit yetersiz (issuer red)
    public const string ProcDoNotHonour = "05";         // onaylanmadı (bilinmeyen kart vb.)
    public const string ProcTimeout = "91";             // issuer zaman aşımı (sentinel)
    public const string ProcAuthError = "99";           // kimlik/biçim hatası (işlem doğmaz)

    // Response alanı
    public const string ResponseApproved = "Approved";
    public const string ResponseDeclined = "Declined";
    public const string ResponseError = "Error";
}
