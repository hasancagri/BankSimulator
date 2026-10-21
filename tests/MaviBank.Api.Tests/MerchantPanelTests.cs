namespace MaviBank.Api.Tests;

// Faz 7 panel: IP whitelist + SecretKey rotasyonu (24s overlap) — saf domain.
public class MerchantPanelTests
{
    [Fact]
    public void IsIpAllowed_BosListe_KisitYok()
        => Assert.True(new Merchant().IsIpAllowed("1.2.3.4"));

    [Fact]
    public void IsIpAllowed_ListeVar_YalnizIzinli()
    {
        var m = new Merchant { AllowedIps = new[] { "10.0.0.1" } };
        Assert.True(m.IsIpAllowed("10.0.0.1"));
        Assert.False(m.IsIpAllowed("9.9.9.9"));
        Assert.False(m.IsIpAllowed(null));
    }

    [Fact]
    public void Reissue_EskiAnahtar_OverlapIcindeGecerli()
    {
        var now = DateTime.UtcNow;
        var m = new Merchant { SecretKey = "OLD" };
        m.ReissueSecretKey("NEW", now, TimeSpan.FromHours(24));

        Assert.Equal("NEW", m.SecretKey);
        var valid = m.ValidSecretKeys(now).ToList();
        Assert.Contains("NEW", valid);
        Assert.Contains("OLD", valid); // overlap içinde eski de geçerli
    }

    [Fact]
    public void Reissue_OverlapSonrasi_EskiGecersiz()
    {
        var now = DateTime.UtcNow;
        var m = new Merchant { SecretKey = "OLD" };
        m.ReissueSecretKey("NEW", now, TimeSpan.FromHours(24));

        var valid = m.ValidSecretKeys(now.AddHours(25)).ToList();
        Assert.Contains("NEW", valid);
        Assert.DoesNotContain("OLD", valid); // 24s sonra eski ölür
    }
}
