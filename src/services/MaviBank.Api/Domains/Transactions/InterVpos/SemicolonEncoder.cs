namespace MaviBank.Api.Domains.Transactions.InterVpos;

// İlke V: InterVpos cevabı `key=value;;key=value` (JSON DEĞİL). Alan SIRASI korunur (çağıran sıralı verir).
// Maskeleme (İlke VI/VIII): ham log'da Pan/Cvv2 tam görünmez.
public static class SemicolonEncoder
{
    /// <summary>Sıralı alanları `key=value;;...` biçiminde birleştirir (saf fonksiyon).</summary>
    public static string Encode(IEnumerable<KeyValuePair<string, string>> fields)
        => string.Join(";;", fields.Select(kv => $"{kv.Key}={kv.Value}"));

    /// <summary>PAN'ı maskeler: ilk 6 + son 4 görünür, ara yıldız. Kısa girdide tümü yıldız.</summary>
    public static string MaskPan(string pan)
    {
        if (string.IsNullOrEmpty(pan)) return string.Empty;
        if (pan.Length <= 10) return new string('*', pan.Length);
        return pan[..6] + new string('*', pan.Length - 10) + pan[^4..];
    }

    /// <summary>CVV'yi tamamen maskeler.</summary>
    public static string MaskCvv(string cvv) => string.IsNullOrEmpty(cvv) ? string.Empty : new string('*', cvv.Length);
}
