namespace Simulator.InterVpos;

// İlke V: InterVpos cevabı `key=value;;key=value` (JSON DEĞİL). Alan SIRASI korunur. Maskeleme Core
// CardMasking'tedir (bank-agnostik).
public static class SemicolonEncoder
{
    public static string Encode(IEnumerable<KeyValuePair<string, string>> fields)
        => string.Join(";;", fields.Select(kv => $"{kv.Key}={kv.Value}"));
}
