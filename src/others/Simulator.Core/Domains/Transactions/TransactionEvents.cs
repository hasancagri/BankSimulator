namespace Simulator.Core.Domains.Transactions;

// İşlem defteri olayları (İlke II event sourcing). Kart verisi İÇERMEZ — yalnız MaskedPan (İlke VI/VIII).

public record TransactionAuthorized(
    string ShopCode, string OrderId, string BatchNo, string TransId, string AuthCode,
    decimal Amount, int Currency, int Installment, string MaskedPan, int CardType, DateTime At);

public record TransactionDeclined(
    string ShopCode, string OrderId, string ProcReturnCode, string ErrorCode, string ErrorMessage,
    decimal Amount, string MaskedPan, DateTime At);

// Faz 2
public record TransactionSettled(
    string BatchNo, decimal DeductionAmount, decimal NetAmount, DateTime BlokeTarihi, DateTime At);

public record TransactionVoided(DateTime At);

public record TransactionRefunded(decimal Amount, decimal RefundedTotal, DateTime At);
