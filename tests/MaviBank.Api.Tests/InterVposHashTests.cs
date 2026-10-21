using System.Security.Cryptography;
using System.Text;

namespace MaviBank.Api.Tests;

// İlke V: 3D hash deterministik + doğrulanabilir (istek + callback).
public class InterVposHashTests
{
    private static string Base64Sha1(string s) => Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(s)));

    [Fact]
    public void RequestHash_AyracsizBirlestirme()
    {
        var h = InterVposHash.ComputeRequestHash("MAVI0001", "ORD-1", "100.00",
            "https://ok", "https://fail", "Auth", "", "RND123", "SECRET");
        Assert.Equal(Base64Sha1("MAVI0001ORD-1100.00https://okhttps://failAuthRND123SECRET"), h);
    }

    [Fact]
    public void RequestHash_Deterministik()
    {
        var a = InterVposHash.ComputeRequestHash("S", "O", "1", "ok", "fail", "Auth", "", "R", "K");
        var b = InterVposHash.ComputeRequestHash("S", "O", "1", "ok", "fail", "Auth", "", "R", "K");
        Assert.Equal(a, b);
    }

    [Fact]
    public void CallbackHash_DegerlerIkiNoktaArtiSecret()
    {
        var values = new[] { "1", "MD1", "ORD-1", "100.00" };
        var h = InterVposHash.ComputeCallbackHash(values, "SECRET");
        Assert.Equal(Base64Sha1("1:MD1:ORD-1:100.00SECRET"), h);
    }

    [Fact]
    public void BuildHashParams_AlanAdlariIkiNokta()
        => Assert.Equal("3DStatus:MD:OrderId", InterVposHash.BuildHashParams(new[] { "3DStatus", "MD", "OrderId" }));

    [Fact]
    public void CallbackHash_MerchantDogrulayabilir()
    {
        // Banka üretir, merchant aynı değerler + aynı SecretKey ile yeniden üretip eşleştirir.
        var values = new[] { "1", "MD9", "ORD-7" };
        var bankHash = InterVposHash.ComputeCallbackHash(values, "SECRET");
        var merchantRecomputed = InterVposHash.ComputeCallbackHash(values, "SECRET");
        Assert.Equal(bankHash, merchantRecomputed);
        Assert.NotEqual(bankHash, InterVposHash.ComputeCallbackHash(values, "WRONG")); // yanlış secret → tutmaz
    }
}
