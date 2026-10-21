using MaviBank.Api.Domains.Onboarding;

namespace MaviBank.Api.Tests;

// Faz 7: onboarding durum makinesi (saf domain).
public class MerchantApplicationTests
{
    private static MerchantApplication Submitted()
        => MerchantApplication.Submit("ACME A.Ş.", "1234567890", "https://acme.example", "retail",
            "TR000000000000000000000000", "info@acme.example", 500000m).Data!;

    [Fact]
    public void Submit_EksikAlan_Red()
    {
        var r = MerchantApplication.Submit("", "1", "w", "s", "iban", "e", 1m);
        Assert.False(r.IsSuccess);
        Assert.Equal(OnboardingConstants.INVALID_APPLICATION, r.Messages[0].Code);
    }

    [Fact]
    public void Advance_LinerIlerler()
    {
        var a = Submitted();
        Assert.Equal(ApplicationStatus.Received, a.Status);
        Assert.True(a.Advance().IsSuccess); Assert.Equal(ApplicationStatus.RiskScreening, a.Status);
        Assert.True(a.Advance().IsSuccess); Assert.Equal(ApplicationStatus.SiteReview, a.Status);
        Assert.True(a.Advance().IsSuccess); Assert.Equal(ApplicationStatus.OfferMade, a.Status);
        Assert.True(a.Advance().IsSuccess); Assert.Equal(ApplicationStatus.ContractSigned, a.Status);
    }

    [Fact]
    public void Advance_SonAdimda_ActivateGerekir()
    {
        var a = Submitted();
        for (var i = 0; i < 4; i++) a.Advance(); // ContractSigned
        var r = a.Advance();
        Assert.False(r.IsSuccess);
        Assert.Equal(OnboardingConstants.CANNOT_ADVANCE, r.Messages[0].Code);
    }

    [Fact]
    public void Activate_YalnizSozlesmeSonrasi()
    {
        var a = Submitted();
        Assert.False(a.Activate("MAVIXX").IsSuccess); // Received'dan aktivasyon olmaz
        for (var i = 0; i < 4; i++) a.Advance();      // ContractSigned
        var r = a.Activate("MAVIXX");
        Assert.True(r.IsSuccess);
        Assert.Equal(ApplicationStatus.Activated, a.Status);
        Assert.Equal("MAVIXX", a.IssuedShopCode);
    }

    [Fact]
    public void Reject_TerminalSonrasiRed()
    {
        var a = Submitted();
        Assert.True(a.Reject().IsSuccess);
        Assert.Equal(ApplicationStatus.Rejected, a.Status);
        Assert.False(a.Reject().IsSuccess); // zaten terminal
    }
}
