
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

// Tek BC = kendi Postgres şeması (Aspire bankDb conn-string'i). Marten Newtonsoft private-setter + nonpublic
// ctor (aggregate/entity deserializasyonu). Transaction event stream + düz doküman (Merchant/Card) birlikte.
var bankDb = builder.Configuration.GetConnectionString("bankDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.BankSchemaName;
        opts.Connection(bankDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s =>
            {
                s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor;
            });

        // İşlem defteri event stream kimliği string: `ShopCode:OrderId` (deterministik idempotency).
        opts.Events.StreamIdentity = JasperFx.Events.StreamIdentity.AsString;

        // Referans dokümanlar (seed) — doğal kimlik.
        opts.Schema.For<Merchant>().Identity(x => x.ShopCode);
        opts.Schema.For<Card>().Identity(x => x.Pan);
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    // Dev: tek düğüm (Solo) — leader election/node-agent koordinasyonu kapalı.
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!)
        .AutoProvision();

    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    // Nested *Handler keşif güvencesi (ad-son-eki tarama nested'i atlayabilir — CLAUDE.md tuzağı).
    opts.Discovery.IncludeType(
        typeof(MaviBank.Api.Domains.Transactions.Features.Commands.Authorize.AuthorizeHandler));
});

builder.Services.AddAllDependencies();

// 001: InterVpos/PaymentLink Options.
builder.Services.AddOptions<MaviBank.Api.Options.InterVposOptions>()
    .BindConfiguration(nameof(MaviBank.Api.Options.InterVposOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<MaviBank.Api.Options.InterVposOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MaviBank.Api.Options.InterVposOptions>>().Value);

var app = builder.Build();

// Startup seed (Merchant/Card referans dokümanları — embedded JSON, idempotent).
await using (var seedScope = app.Services.CreateAsyncScope())
{
    var seedSession = seedScope.ServiceProvider.GetRequiredService<IDocumentSession>();
    await MerchantSeeder.SeedAsync(seedSession);
    await CardSeeder.SeedAsync(seedSession);
}

app.MapDefaultEndpoints();
app.MapScalarDocumentation();

// Faz 1: InterVpos ödeme yüzü (POST /mpi/Default.aspx, TxnType=Auth NonSecure).
app.AddInterVposEndpoints();

await app.RunAsync();
