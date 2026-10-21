using System.Reflection;

namespace MaviBank.Api.Domains.Merchants;

// Üye-işyeri seed'i (embedded JSON → upsert, idempotent; 090 BinCardSeeder deseni). Onboarding Faz 7.
public static class MerchantSeeder
{
    private const string ResourceSuffix = "Domains.Merchants.merchant-seed.json";

    public static async Task SeedAsync(IDocumentSession session, CancellationToken ct = default)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        if (name is null) return;

        await using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null) return;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(ct);
        var merchants = JsonConvert.DeserializeObject<List<Merchant>>(json);
        if (merchants is null || merchants.Count == 0) return;

        foreach (var m in merchants)
            session.Store(m);
        await session.SaveChangesAsync(ct);
    }
}
