namespace MaviBank.Api.Domains.Accounts;

/// <summary>
/// Faz 6 — üye-işyeri POS alacak hesabı (düz Marten doküman; kimlik ShopCode). Settlement net tutarı
/// BLOKELİ (pending) ekler; bloke/valör süresi dolunca `ReleaseMatured` ile KULLANILABİLİR (available)'a
/// geçer ("POS alacağı: net tutar düştü"). "1000 sattım, 950 ne zaman düştü" döngüsünü kapatır (gereksinim §6).
/// Koleksiyon private (İlke II). Gerçek para yok — simülasyon.
/// </summary>
public class Account
{
    private Account() { }

    public string ShopCode { get; private set; } = string.Empty; // kimlik
    public decimal AvailableBalance { get; private set; }
    public decimal PendingBalance { get; private set; }

    private readonly List<PendingEntry> _pending = new();
    public IReadOnlyList<PendingEntry> Pending => _pending;

    private readonly List<AccountMovement> _movements = new();
    public IReadOnlyList<AccountMovement> Movements => _movements;

    public static Account OpenFor(string shopCode) => new() { ShopCode = shopCode };

    /// <summary>Settlement net tutarını blokeli ekler (valör tarihine kadar pending).</summary>
    public void AddPending(string orderId, string batchNo, decimal net, DateTime blokeTarihi, DateTime now)
    {
        _pending.Add(new PendingEntry { OrderId = orderId, BatchNo = batchNo, Net = net, BlokeTarihi = blokeTarihi });
        PendingBalance += net;
        _movements.Add(new AccountMovement
        {
            At = now, Type = "SETTLEMENT_PENDING", Amount = net, OrderId = orderId,
            Note = $"Bloke valör: {blokeTarihi:yyyy-MM-dd}"
        });
    }

    /// <summary>Valörü dolan (BlokeTarihi &lt;= now) blokeli tutarları kullanılabilire geçirir. Düşen kayıt sayısı.</summary>
    public int ReleaseMatured(DateTime now)
    {
        var count = 0;
        foreach (var p in _pending.Where(p => !p.Released && p.BlokeTarihi <= now))
        {
            p.Released = true;
            PendingBalance -= p.Net;
            AvailableBalance += p.Net;
            _movements.Add(new AccountMovement
            {
                At = now, Type = "POS_CREDIT", Amount = p.Net, OrderId = p.OrderId,
                Note = "POS alacagi hesaba dustu"
            });
            count++;
        }
        return count;
    }
}

// Blokeli settlement kalemi (Account içinde nested; Newtonsoft serialize).
public class PendingEntry
{
    public string OrderId { get; set; } = string.Empty;
    public string BatchNo { get; set; } = string.Empty;
    public decimal Net { get; set; }
    public DateTime BlokeTarihi { get; set; }
    public bool Released { get; set; }
}

// Hesap hareketi (ekstre satırı).
public class AccountMovement
{
    public DateTime At { get; set; }
    public string Type { get; set; } = string.Empty;   // SETTLEMENT_PENDING / POS_CREDIT
    public decimal Amount { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}
