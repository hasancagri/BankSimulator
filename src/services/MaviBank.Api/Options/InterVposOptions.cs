using System.ComponentModel.DataAnnotations;

namespace MaviBank.Api.Options;

// InterVpos ödeme yüzünün kullanıcıdan gelmeyen sabitleri (handler'da literal yasak — İlke VII/conventions).
// Hepsi non-secret (appsettings). SecretKey vb. sırlar burada DEĞİL (Merchant seed + secret-store).
public class InterVposOptions
{
    /// <summary>Desteklenen para birimi (ISO numerik). v1 yalnız TL = 949.</summary>
    [Required]
    public int SupportedCurrency { get; set; } = 949;

    /// <summary>Varsayılan dil (cevap/log bağlamı).</summary>
    [Required]
    public string DefaultLang { get; set; } = "tr";
}
