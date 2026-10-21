namespace MaviBank.Api.Tests;

// Faz 2 domain: Batch yaşam döngüsü + Merchant komisyon oranı (saf).
public class BatchAndCommissionTests
{
    [Fact]
    public void Batch_OpenFor_AcikVeMerchant()
    {
        var b = Batch.OpenFor("MAVI0001", DateTime.UtcNow);
        Assert.Equal(BatchStatus.Open, b.Status);
        Assert.Equal("MAVI0001", b.ShopCode);
        Assert.StartsWith("MAVI0001-", b.BatchNo);
        Assert.Empty(b.TransactionKeys);
    }

    [Fact]
    public void Batch_AddRemove_Idempotent()
    {
        var b = Batch.OpenFor("MAVI0001", DateTime.UtcNow);
        b.AddTransaction("MAVI0001:ORD-1");
        b.AddTransaction("MAVI0001:ORD-1"); // tekrar eklenmez
        Assert.Single(b.TransactionKeys);
        b.RemoveTransaction("MAVI0001:ORD-1");
        Assert.Empty(b.TransactionKeys);
    }

    [Fact]
    public void Batch_Close_ToplamVeDurum()
    {
        var b = Batch.OpenFor("MAVI0001", DateTime.UtcNow);
        b.Close(250m, 4.48m, 245.52m, DateTime.UtcNow);
        Assert.Equal(BatchStatus.Closed, b.Status);
        Assert.Equal(250m, b.GrossTotal);
        Assert.Equal(245.52m, b.NetTotal);
        Assert.NotNull(b.ClosedAt);
        Assert.NotNull(b.EndOfDayDate);
    }

    [Fact]
    public void Merchant_RateFor_TabloVeVarsayilan()
    {
        var m = new Merchant
        {
            CommissionRates = new Dictionary<int, decimal> { { 1, 0.0179m }, { 3, 0.0219m } },
            DefaultCommissionRate = 0.0179m
        };
        Assert.Equal(0.0219m, m.RateFor(3));   // tablodan
        Assert.Equal(0.0179m, m.RateFor(9));   // tabloda yok → varsayılan
    }
}
