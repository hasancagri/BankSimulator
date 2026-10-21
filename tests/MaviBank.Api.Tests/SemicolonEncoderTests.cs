namespace MaviBank.Api.Tests;

// İlke V/VI: `;;` encode birebir + PAN/CVV maskeleme (domain-TDD).
public class SemicolonEncoderTests
{
    [Fact]
    public void Encode_SiraliAlanlar_SemicolonAyracli()
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("ProcReturnCode", "00"),
            new("OrderId", "ORD-1"),
            new("TransId", "ABC123")
        };
        Assert.Equal("ProcReturnCode=00;;OrderId=ORD-1;;TransId=ABC123", SemicolonEncoder.Encode(fields));
    }

    [Fact]
    public void MaskPan_Ilk6Son4Gorunur()
        => Assert.Equal("411111******0001", CardMasking.MaskPan("4111111111110001"));

    [Fact]
    public void MaskPan_KisaGirdi_TumuYildiz()
        => Assert.Equal("******", CardMasking.MaskPan("123456"));

    [Fact]
    public void MaskCvv_TumuYildiz()
        => Assert.Equal("***", CardMasking.MaskCvv("123"));
}
