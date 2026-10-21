namespace Simulator.Core.Constants;

// Normalize cevap kodları (bank-agnostik domain değerleri; facade'ler tele render eder — InterVpos `;;`,
// TuruncuBank XML). "00" onay, diğerleri red. ISO-vari.
public static class ResponseCodes
{
    public const string Approved = "00";
    public const string InsufficientFunds = "51";
    public const string DoNotHonour = "05";
    public const string Timeout = "91";
    public const string AuthError = "99";
}
