
namespace MaviBank.Api.Tests;

// Faz 6: hesaba düşüş — settlement blokeli ekler, valör dolunca kullanılabilire geçer.
public class AccountTests
{
    [Fact]
    public void AddPending_BlokeliBakiyeArtar()
    {
        var a = Account.OpenFor("MAVI0001");
        a.AddPending("ORD-1", "B1", 98.21m, DateTime.UtcNow.Date.AddDays(1), DateTime.UtcNow);
        Assert.Equal(98.21m, a.PendingBalance);
        Assert.Equal(0m, a.AvailableBalance);
        Assert.Single(a.Pending);
    }

    [Fact]
    public void ReleaseMatured_YalnizValoruDolan_Gecer()
    {
        var now = DateTime.UtcNow;
        var a = Account.OpenFor("MAVI0001");
        a.AddPending("ORD-1", "B1", 100m, now.AddDays(-1), now);  // valörü dolmuş
        a.AddPending("ORD-2", "B1", 50m, now.AddDays(5), now);    // daha dolmamış

        var count = a.ReleaseMatured(now);

        Assert.Equal(1, count);
        Assert.Equal(100m, a.AvailableBalance); // yalnız dolmuş geçti
        Assert.Equal(50m, a.PendingBalance);    // dolmamış blokede kaldı
    }

    [Fact]
    public void ReleaseMatured_TekrarCalisirsa_CiftGecirmez()
    {
        var now = DateTime.UtcNow;
        var a = Account.OpenFor("MAVI0001");
        a.AddPending("ORD-1", "B1", 100m, now.AddDays(-1), now);
        a.ReleaseMatured(now);
        var second = a.ReleaseMatured(now); // idempotent — zaten released
        Assert.Equal(0, second);
        Assert.Equal(100m, a.AvailableBalance);
        Assert.Equal(0m, a.PendingBalance);
    }
}
