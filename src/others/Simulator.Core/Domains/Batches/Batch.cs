namespace Simulator.Core.Domains.Batches;

/// <summary>
/// Gün-sonu batch'i — düz Marten doküman (koordinasyon kaydı, event stream değil). Merchant başına tek AÇIK
/// batch (aktif-tekil; Program.cs partial-unique index). Onaylı işlem açık batch'e yazılır (TransactionKeys);
/// Void çıkarır; gün sonu kapatır (totals) + yeni açılır. Koleksiyon private (İlke II).
/// </summary>
public class Batch
{
    // Marten Newtonsoft private-setter + nonpublic ctor.
    private Batch() { }

    public string BatchNo { get; private set; } = string.Empty; // kimlik
    public string ShopCode { get; private set; } = string.Empty;
    public BatchStatus Status { get; private set; }
    public DateTime OpenedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public DateTime? EndOfDayDate { get; private set; }

    private readonly List<string> _transactionKeys = new();
    public IReadOnlyList<string> TransactionKeys => _transactionKeys;

    public decimal GrossTotal { get; private set; }
    public decimal DeductionTotal { get; private set; }
    public decimal NetTotal { get; private set; }

    /// <summary>Yeni açık batch (merchant başına). BatchNo = ShopCode + kısa benzersiz.</summary>
    public static Batch OpenFor(string shopCode, DateTime now)
        => new()
        {
            BatchNo = $"{shopCode}-{Guid.NewGuid():N}"[..(shopCode.Length + 9)],
            ShopCode = shopCode,
            Status = BatchStatus.Open,
            OpenedAt = now
        };

    /// <summary>Onaylı işlemin stream anahtarını açık batch'e ekler (idempotent).</summary>
    public void AddTransaction(string key)
    {
        if (!_transactionKeys.Contains(key)) _transactionKeys.Add(key);
    }

    /// <summary>Void edilen işlemi batch'ten çıkarır (iz bırakmaz).</summary>
    public void RemoveTransaction(string key) => _transactionKeys.Remove(key);

    /// <summary>Gün sonu: batch'i kapatır, toplamları mühürler.</summary>
    public void Close(decimal grossTotal, decimal deductionTotal, decimal netTotal, DateTime now)
    {
        Status = BatchStatus.Closed;
        ClosedAt = now;
        EndOfDayDate = now.Date;
        GrossTotal = grossTotal;
        DeductionTotal = deductionTotal;
        NetTotal = netTotal;
    }
}

public enum BatchStatus
{
    Open = 0,
    Closed = 1
}
