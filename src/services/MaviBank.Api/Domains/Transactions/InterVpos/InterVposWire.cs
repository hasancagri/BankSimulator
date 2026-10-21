namespace MaviBank.Api.Domains.Transactions.InterVpos;

// İlke V/VII: InterVpos wire tipi — facade içinde nested, domain sınırını geçmez; transient (saklanmaz).
// İstek form-urlencoded alanlarının normalize hali. Cevap `;;` olarak endpoint'te sıralı kurulur.
public sealed record AuthRequestWire(
    string ShopCode, string UserCode, string UserPass, string TxnType, string SecureType,
    string Pan, string Expiry, string Cvv2, decimal PurchAmount, int Currency,
    string OrderId, int InstallmentCount, int CardType);
