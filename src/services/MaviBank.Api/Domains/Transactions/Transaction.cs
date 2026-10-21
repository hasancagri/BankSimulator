namespace MaviBank.Api.Domains.Transactions;

/// <summary>
/// İşlem defteri aggregate'i — Marten event stream (append-only; durum olaylardan türetilir). Stream kimliği
/// string: `ShopCode:OrderId`. Kart verisi TAŞIMAZ (MaskedPan). Yaşam döngüsü: Authorized → (Void) Voided |
/// (gün sonu) Settled → (Refund kısmi) Settled+RefundedAmount. Void/Refund açık/kapalı batch ayrımı duruma
/// yansır (Authorized=açık batch, Settled=kapalı batch).
/// </summary>
public class Transaction
{
    public string Id { get; set; } = string.Empty; // stream key (ShopCode:OrderId)
    public string ShopCode { get; private set; } = string.Empty;
    public string OrderId { get; private set; } = string.Empty;
    public string BatchNo { get; private set; } = string.Empty;
    public string? TransId { get; private set; }
    public string? AuthCode { get; private set; }
    public decimal Amount { get; private set; }
    public int Currency { get; private set; }
    public int Installment { get; private set; }
    public string MaskedPan { get; private set; } = string.Empty;
    public string? ProcReturnCode { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public decimal RefundedAmount { get; private set; }
    public decimal DeductionAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public DateTime? BlokeTarihi { get; private set; }
    public DateTime? VoidDate { get; private set; }
    public TransactionStatus Status { get; private set; }

    public void Apply(TransactionAuthorized e)
    {
        ShopCode = e.ShopCode;
        OrderId = e.OrderId;
        BatchNo = e.BatchNo;
        TransId = e.TransId;
        AuthCode = e.AuthCode;
        Amount = e.Amount;
        Currency = e.Currency;
        Installment = e.Installment;
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

    public void Apply(TransactionSettled e)
    {
        BatchNo = e.BatchNo;
        DeductionAmount = e.DeductionAmount;
        NetAmount = e.NetAmount;
        BlokeTarihi = e.BlokeTarihi;
        Status = TransactionStatus.Settled;
    }

    public void Apply(TransactionVoided e)
    {
        VoidDate = e.At;
        Status = TransactionStatus.Voided;
    }

    public void Apply(TransactionRefunded e)
    {
        RefundedAmount = e.RefundedTotal; // birikimli toplam (olay taşır)
        // Durum Settled kalır (kısmi/tam iade); RefundedAmount izler.
    }

    /// <summary>İptal edilebilir mi — yalnız açık batch (Authorized). Settled/Voided/Declined → hayır.</summary>
    public bool CanVoid() => Status == TransactionStatus.Authorized;

    /// <summary>İade edilebilir mi — yalnız kapalı batch (Settled) + birikimli iade tutarı aşmamalı.</summary>
    public bool CanRefund(decimal amount)
        => Status == TransactionStatus.Settled && amount > 0 && RefundedAmount + amount <= Amount;
}

// Enum aggregate dosyasında (conventions).
public enum TransactionStatus
{
    Authorized = 0,
    Declined = 1,
    Voided = 2,
    Settled = 3
}
