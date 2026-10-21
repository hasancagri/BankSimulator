namespace MaviBank.Api.Domains.Transactions;

/// <summary>
/// İşlem defteri aggregate'i — Marten event stream (append-only; güncel durum olaylardan türetilir).
/// Stream kimliği string: `ShopCode:OrderId` (deterministik → `(ShopCode,OrderId)` idempotency). Kart
/// verisi TAŞIMAZ (yalnız MaskedPan — İlke VI/VIII). Faz 1: Authorized/Declined. Faz 2: Capture/Void/Refund.
/// </summary>
public class Transaction
{
    public string Id { get; set; } = string.Empty; // stream key (ShopCode:OrderId)
    public string ShopCode { get; private set; } = string.Empty;
    public string OrderId { get; private set; } = string.Empty;
    public string? TransId { get; private set; }
    public string? AuthCode { get; private set; }
    public decimal Amount { get; private set; }
    public int Currency { get; private set; }
    public string MaskedPan { get; private set; } = string.Empty;
    public string? ProcReturnCode { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public TransactionStatus Status { get; private set; }

    public void Apply(TransactionAuthorized e)
    {
        ShopCode = e.ShopCode;
        OrderId = e.OrderId;
        TransId = e.TransId;
        AuthCode = e.AuthCode;
        Amount = e.Amount;
        Currency = e.Currency;
        MaskedPan = e.MaskedPan;
        ProcReturnCode = InterVpos.InterVposConstants.ProcApproved;
        Status = TransactionStatus.Authorized;
    }

    public void Apply(TransactionDeclined e)
    {
        ShopCode = e.ShopCode;
        OrderId = e.OrderId;
        Amount = e.Amount;
        MaskedPan = e.MaskedPan;
        ProcReturnCode = e.ProcReturnCode;
        ErrorCode = e.ErrorCode;
        ErrorMessage = e.ErrorMessage;
        Status = TransactionStatus.Declined;
    }
}

// Enum aggregate dosyasında (conventions). Faz 2: Captured/Voided/Refunded eklenir.
public enum TransactionStatus
{
    Authorized = 0,
    Declined = 1
}
