using System.Security.Cryptography;
using System.Text;

namespace TuruncuBank.Api.TuruncuPos;

/// <summary>
/// TuruncuBank protokol hash'i — İlke VII'nin ispatı: MaviBank/InterVpos SHA1 + `;;`; TuruncuBank **SHA512 +
/// XML**. Aynı Core, FARKLI tel. Hash = base64(SHA512(ShopCode + OrderId + Amount + SecretKey)). Deterministik.
/// </summary>
public static class TuruncuHash
{
    public static string Compute(string shopCode, string orderId, string amount, string secretKey)
        => Convert.ToBase64String(SHA512.HashData(Encoding.UTF8.GetBytes(shopCode + orderId + amount + secretKey)));
}
