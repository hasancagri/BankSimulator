namespace MaviBank.Api.Domains.Onboarding;

/// <summary>
/// Faz 7 — üye-işyeri başvuru aggregate'i (düz Marten doküman; kimlik Id). Değerlendirme durum makinesi:
/// Received → RiskScreening → SiteReview → OfferMade → ContractSigned → (Activate) Activated | Rejected.
/// Admin adımları ilerletir/reddeder; aktivasyonda teknik credential üretilir (Merchant doğar). Geçişler
/// aggregate metodunda guard'lı (İlke II/IV). Sertifikasyon kapısı + panel + IP whitelist seam (sonraki).
/// </summary>
public class MerchantApplication
{
    private MerchantApplication() { }

    public string Id { get; private set; } = string.Empty;
    public string CompanyTitle { get; private set; } = string.Empty;
    public string TaxNo { get; private set; } = string.Empty;
    public string Website { get; private set; } = string.Empty;
    public string Sector { get; private set; } = string.Empty;
    public string Iban { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public decimal EstimatedTurnover { get; private set; }
    public ApplicationStatus Status { get; private set; }
    public string? IssuedShopCode { get; private set; }
    public DateTime CreatedTime { get; private set; }
    public DateTime? UpdatedTime { get; private set; }

    /// <summary>Başvuru alınır (Received). Zorunlu alanlar dolu olmalı.</summary>
    public static ResultDomain<MerchantApplication> Submit(string companyTitle, string taxNo, string website,
        string sector, string iban, string contactEmail, decimal estimatedTurnover)
    {
        if (string.IsNullOrWhiteSpace(companyTitle) || string.IsNullOrWhiteSpace(taxNo) ||
            string.IsNullOrWhiteSpace(website) || string.IsNullOrWhiteSpace(iban) ||
            string.IsNullOrWhiteSpace(contactEmail))
            return ResultDomain<MerchantApplication>.Error(new MessageItem { Code = OnboardingConstants.INVALID_APPLICATION });

        return ResultDomain<MerchantApplication>.Ok(new MerchantApplication
        {
            Id = Guid.NewGuid().ToString("N"),
            CompanyTitle = companyTitle, TaxNo = taxNo, Website = website, Sector = sector,
            Iban = iban, ContactEmail = contactEmail, EstimatedTurnover = estimatedTurnover,
            Status = ApplicationStatus.Received, CreatedTime = DateTime.UtcNow
        });
    }

    /// <summary>Değerlendirmeyi bir sonraki adıma ilerletir (admin). Son adımda Activate gerekir.</summary>
    public ResultDomain Advance()
    {
        if (Status is ApplicationStatus.Activated or ApplicationStatus.Rejected)
            return ResultDomain.Error(new MessageItem { Code = OnboardingConstants.APPLICATION_TERMINAL });

        var next = Status switch
        {
            ApplicationStatus.Received => ApplicationStatus.RiskScreening,
            ApplicationStatus.RiskScreening => ApplicationStatus.SiteReview,
            ApplicationStatus.SiteReview => ApplicationStatus.OfferMade,
            ApplicationStatus.OfferMade => ApplicationStatus.ContractSigned,
            _ => (ApplicationStatus?)null
        };
        if (next is null)
            return ResultDomain.Error(new MessageItem { Code = OnboardingConstants.CANNOT_ADVANCE });

        Status = next.Value;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Başvuru reddedilir (terminal). Zaten terminal → hata.</summary>
    public ResultDomain Reject()
    {
        if (Status is ApplicationStatus.Activated or ApplicationStatus.Rejected)
            return ResultDomain.Error(new MessageItem { Code = OnboardingConstants.APPLICATION_TERMINAL });
        Status = ApplicationStatus.Rejected;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }

    /// <summary>Teknik aktivasyon: yalnız sözleşme imzalandıysa. Üretilen ShopCode bağlanır, Activated.</summary>
    public ResultDomain Activate(string issuedShopCode)
    {
        if (Status != ApplicationStatus.ContractSigned)
            return ResultDomain.Error(new MessageItem { Code = OnboardingConstants.NOT_READY_TO_ACTIVATE });
        Status = ApplicationStatus.Activated;
        IssuedShopCode = issuedShopCode;
        UpdatedTime = DateTime.UtcNow;
        return ResultDomain.Ok();
    }
}

public enum ApplicationStatus
{
    Received = 0,
    RiskScreening = 1,
    SiteReview = 2,
    OfferMade = 3,
    ContractSigned = 4,
    Activated = 5,
    Rejected = 6
}
