namespace Simulator.Core.Constants;

// Kart maskeleme (İlke VI/VIII) — bank-agnostik Core util. Tam PAN/CVV hiçbir kalıcı ortamda/log'da görünmez.
public static class CardMasking
{
    /// <summary>PAN: ilk 6 + son 4 görünür, ara yıldız; kısa girdide tümü yıldız.</summary>
    public static string MaskPan(string pan)
    {
        if (string.IsNullOrEmpty(pan)) return string.Empty;
        if (pan.Length <= 10) return new string('*', pan.Length);
        return pan[..6] + new string('*', pan.Length - 10) + pan[^4..];
    }

    public static string MaskCvv(string cvv) => string.IsNullOrEmpty(cvv) ? string.Empty : new string('*', cvv.Length);
}
