using System.Security.Cryptography;
using System.Text;

namespace Simulator.InterVpos;

/// <summary>
/// İlke V: InterVpos 3D hash (gerçek protokol birebir, Ders 2). İki yön:
/// (1) İstek hash'i — merchant üretir, sim doğrular: `SHA1(ShopCode+OrderId+PurchAmount+OkUrl+FailUrl+
///     TxnType+InstallmentCount+Rnd+SecretKey)` → base64 (AYRAÇSIZ birleştirme).
/// (2) Callback hash'i — sim üretir (bankanın imzası), merchant doğrular: HASHPARAMS alan adları ':' ile;
///     HASH = `base64(SHA1( değerler(':' ile) + SecretKey ))`.
/// SecretKey sırdır — hash girdisine girer ama hiçbir çıktıda görünmez (İlke VI/VIII).
/// </summary>
public static class InterVposHash
{
    public static string ComputeRequestHash(string shopCode, string orderId, string purchAmount,
        string okUrl, string failUrl, string txnType, string installment, string rnd, string secretKey)
        => Base64Sha1(shopCode + orderId + purchAmount + okUrl + failUrl + txnType + installment + rnd + secretKey);

    /// <summary>Callback HASH — değerler ':' ile birleşir + SecretKey → SHA1 → base64.</summary>
    public static string ComputeCallbackHash(IEnumerable<string> values, string secretKey)
        => Base64Sha1(string.Join(":", values) + secretKey);

    /// <summary>HASHPARAMS — imzalanan alan adları ':' ile.</summary>
    public static string BuildHashParams(IEnumerable<string> names) => string.Join(":", names);

    private static string Base64Sha1(string input)
        => Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(input)));
}
