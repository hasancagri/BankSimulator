namespace MaviBank.Api.Domains.Transactions;

// İşlem defteri olayları (İlke II event sourcing). Kart verisi İÇERMEZ — yalnız MaskedPan (İlke VI/VIII).
// Faz 2'de Captured/Voided/Refunded aynı stream'e eklenir.

public record TransactionAuthorized(
    string ShopCode, string OrderId, string TransId, string AuthCode,
    decimal Amount, int Currency, int Installment, string MaskedPan, int CardType, DateTime At);

public record TransactionDeclined(
    string ShopCode, string OrderId, string ProcReturnCode, string ErrorCode, string ErrorMessage,
    decimal Amount, string MaskedPan, DateTime At);
