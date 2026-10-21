namespace MaviBank.Api.Tests;

// İlke II/VIII: Transaction olay uygulaması (Apply) + yaşam döngüsü guard'ları (CanVoid/CanRefund).
public class TransactionTests
{
    private static Transaction Authorized(decimal amount = 125.50m)
    {
        var tx = new Transaction();
        tx.Apply(new TransactionAuthorized("MAVI0001", "ORD-1", "MAVI0001-ab12cd34", "TID1", "P00ABC",
            amount, 949, 0, "411111******0001", 0, DateTime.UtcNow));
        return tx;
    }

    [Fact]
    public void Apply_Authorized_DurumVeAlanlar()
    {
        var tx = Authorized();
        Assert.Equal(TransactionStatus.Authorized, tx.Status);
        Assert.Equal("MAVI0001-ab12cd34", tx.BatchNo);
        Assert.Equal("TID1", tx.TransId);
        Assert.Equal("411111******0001", tx.MaskedPan);
        Assert.DoesNotContain("4111111111110001", tx.MaskedPan);
    }

    [Fact]
    public void Apply_Declined_DurumVeRedKodu()
    {
        var tx = new Transaction();
        tx.Apply(new TransactionDeclined("MAVI0001", "ORD-2", "51", "51", "Yetersiz bakiye", 10m,
            "411111******0002", DateTime.UtcNow));
        Assert.Equal(TransactionStatus.Declined, tx.Status);
        Assert.Equal("51", tx.ProcReturnCode);
        Assert.Null(tx.AuthCode);
    }

    // --- Faz 2: Void guard ---

    [Fact]
    public void CanVoid_Authorized_True() => Assert.True(Authorized().CanVoid());

    [Fact]
    public void Apply_Voided_DurumVeTarih()
    {
        var tx = Authorized();
        tx.Apply(new TransactionVoided(DateTime.UtcNow));
        Assert.Equal(TransactionStatus.Voided, tx.Status);
        Assert.NotNull(tx.VoidDate);
        Assert.False(tx.CanVoid()); // ikinci void edilemez
    }

    // --- Faz 2: Settlement + Refund guard ---

    private static Transaction Settled(decimal amount = 100m)
    {
        var tx = Authorized(amount);
        tx.Apply(new TransactionSettled("MAVI0001-ab12cd34", 1.79m, amount - 1.79m,
            DateTime.UtcNow.Date.AddDays(1), DateTime.UtcNow));
        return tx;
    }

    [Fact]
    public void Apply_Settled_NetVeBloke()
    {
        var tx = Settled(100m);
        Assert.Equal(TransactionStatus.Settled, tx.Status);
        Assert.Equal(1.79m, tx.DeductionAmount);
        Assert.Equal(98.21m, tx.NetAmount);
        Assert.NotNull(tx.BlokeTarihi);
    }

    [Fact]
    public void CanVoid_Settled_False() => Assert.False(Settled().CanVoid()); // gün sonu geçti

    [Fact]
    public void CanRefund_Settled_KismiVeAsim()
    {
        var tx = Settled(100m);
        Assert.True(tx.CanRefund(30m));
        Assert.False(tx.CanRefund(0m));     // sıfır/negatif red
        Assert.True(tx.CanRefund(100m));    // tam iade sınırı

        tx.Apply(new TransactionRefunded(30m, 30m, DateTime.UtcNow));
        Assert.Equal(30m, tx.RefundedAmount);
        Assert.True(tx.CanRefund(70m));     // kalan tam
        Assert.False(tx.CanRefund(71m));    // aşım red
    }

    [Fact]
    public void CanRefund_Authorized_False()
        => Assert.False(Authorized().CanRefund(10m)); // gün sonu görmemiş, iade edilemez
}
