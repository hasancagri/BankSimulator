namespace MaviBank.Api.Tests;

// İlke VI: sentinel kart → deterministik ChargeOutcome (gerçek bakiye yok).
public class ChargeScenarioTests
{
    private static readonly ChargeScenarioEngine Engine = new();

    private static Card CardWith(CardScenario s) => new() { Pan = "4111111111110000", Scenario = s };

    [Fact]
    public void Approve_OnayVeAuthCode()
    {
        var o = Engine.Evaluate(CardWith(CardScenario.Approve));
        Assert.True(o.IsApproved);
        Assert.True(o.CreatesTransaction);
        Assert.Equal(InterVposConstants.ProcApproved, o.ProcReturnCode);
        Assert.False(string.IsNullOrWhiteSpace(o.AuthCode));
    }

    [Fact]
    public void InsufficientFunds_Red51()
    {
        var o = Engine.Evaluate(CardWith(CardScenario.InsufficientFunds));
        Assert.False(o.IsApproved);
        Assert.True(o.CreatesTransaction);                       // issuer-red bir denemedir (defterde kalır)
        Assert.Equal(InterVposConstants.ProcInsufficientFunds, o.ProcReturnCode);
    }

    [Fact]
    public void Timeout_Red91()
    {
        var o = Engine.Evaluate(CardWith(CardScenario.Timeout));
        Assert.False(o.IsApproved);
        Assert.Equal(InterVposConstants.ProcTimeout, o.ProcReturnCode);
    }

    [Fact]
    public void BilinmeyenKart_DoNotHonour()
    {
        var o = Engine.Evaluate(null);
        Assert.False(o.IsApproved);
        Assert.Equal(InterVposConstants.ProcDoNotHonour, o.ProcReturnCode);
    }
}
