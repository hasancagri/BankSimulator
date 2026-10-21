namespace Simulator.Core.Domains.Merchants;

// Üye-işyeri seed'i — her banka HOST'u kendi merchant-seed.json'unu embed eder; seeder host assembly'sinden
// okur (banka başına ayrı merchant seti, ayrı DB). Idempotent upsert (090 BinCardSeeder deseni).
public static class MerchantSeeder
{
    public static async Task SeedAsync(IDocumentSession session, Assembly assembly, CancellationToken ct = default)
    {
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("merchant-seed.json", StringComparison.Ordinal));
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
