namespace MaviBank.Api.Tests;

// İlke II/VIII: Transaction olay uygulaması (Apply) + kart verisi taşımaz (yalnız MaskedPan).
public class TransactionTests
{
    [Fact]
    public void Apply_Authorized_DurumVeAlanlar()
    {
        var tx = new Transaction();
        tx.Apply(new TransactionAuthorized("MAVI0001", "ORD-1", "TID1", "P00ABC", 125.50m, 949, 0,
            "411111******0001", 0, DateTime.UtcNow));

        Assert.Equal(TransactionStatus.Authorized, tx.Status);
        Assert.Equal("MAVI0001", tx.ShopCode);
        Assert.Equal("ORD-1", tx.OrderId);
        Assert.Equal("TID1", tx.TransId);
        Assert.Equal("P00ABC", tx.AuthCode);
        Assert.Equal(125.50m, tx.Amount);
        Assert.Equal("411111******0001", tx.MaskedPan);
        Assert.DoesNotContain("4111111111110001", tx.MaskedPan); // tam PAN yok
    }

    [Fact]
    public void Apply_Declined_DurumVeRedKodu()
    {
        var tx = new Transaction();
        tx.Apply(new TransactionDeclined("MAVI0001", "ORD-2", "51", "51", "Yetersiz bakiye", 10m,
            "411111******0002", DateTime.UtcNow));

        Assert.Equal(TransactionStatus.Declined, tx.Status);
        Assert.Equal("51", tx.ProcReturnCode);
        Assert.Equal("51", tx.ErrorCode);
        Assert.Null(tx.AuthCode);
    }
}
