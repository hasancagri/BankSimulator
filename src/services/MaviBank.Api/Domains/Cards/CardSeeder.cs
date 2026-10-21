using System.Reflection;

namespace MaviBank.Api.Domains.Cards;

// Test kartı seed'i (embedded JSON → upsert, idempotent). Scenario string → enum (Newtonsoft ad eşleşmesi).
public static class CardSeeder
{
    private const string ResourceSuffix = "Domains.Cards.card-seed.json";

    public static async Task SeedAsync(IDocumentSession session, CancellationToken ct = default)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(ResourceSuffix, StringComparison.Ordinal));
        if (name is null) return;

        await using var stream = assembly.GetManifestResourceStream(name);
        if (stream is null) return;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync(ct);
        var cards = JsonConvert.DeserializeObject<List<Card>>(json);
        if (cards is null || cards.Count == 0) return;

        foreach (var c in cards)
            session.Store(c);
        await session.SaveChangesAsync(ct);
    }
}
