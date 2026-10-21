namespace MaviBank.Api.Domains.Onboarding;

// Onboarding hata kodları (Result pattern — Code sabit, serbest metin yasak).
public static class OnboardingConstants
{
    public static readonly string INVALID_APPLICATION = "ONBOARDING_INVALID_APPLICATION";   // zorunlu alan eksik
    public static readonly string APPLICATION_TERMINAL = "ONBOARDING_APPLICATION_TERMINAL";  // Activated/Rejected'tan geçiş
    public static readonly string CANNOT_ADVANCE = "ONBOARDING_CANNOT_ADVANCE";              // son adımda (Activate gerekir)
    public static readonly string NOT_READY_TO_ACTIVATE = "ONBOARDING_NOT_READY";            // sözleşme imzalanmadan aktivasyon
    public static readonly string APPLICATION_NOT_FOUND = "ONBOARDING_NOT_FOUND";
}
