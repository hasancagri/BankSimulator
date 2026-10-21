
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

// TuruncuBank host — kendi şeması (turuncubank). FARKLI protokol facade (XML + SHA512); Core domain aynen
// paylaşılır (İlke VII: aynı Authorize/Transaction/Batch, ayrı tel). InterVpos facade YOK.
var bankDb = builder.Configuration.GetConnectionString("turuncubankDb")!;
builder.Services.AddMarten(opts =>
    {
        opts.DatabaseSchemaName = SchemaConstants.BankSchemaName;
        opts.Connection(bankDb);
        opts.UseNewtonsoftForSerialization(
            nonPublicMembersStorage: NonPublicMembersStorage.NonPublicSetters,
            configure: s => { s.ConstructorHandling = Newtonsoft.Json.ConstructorHandling.AllowNonPublicDefaultConstructor; });
        opts.ConfigureCoreSchema();
    })
    .IntegrateWithWolverine()
    .ApplyAllDatabaseChangesOnStartup();

builder.Host.UseWolverine(opts =>
{
    if (builder.Environment.IsDevelopment())
        opts.Durability.Mode = DurabilityMode.Solo;

    opts.UseRabbitMq(builder.Configuration.GetConnectionString("rabbitmq")!).AutoProvision();
    opts.Policies.UseDurableLocalQueues();
    opts.Discovery.IncludeAssembly(Assembly.GetExecutingAssembly());
    opts.IncludeCoreHandlers(); // Core handler'ları (InterVpos 3DS yok)
});

builder.Services.AddAllDependencies();

builder.Services.AddOptions<InterVposOptions>()
    .BindConfiguration(nameof(InterVposOptions))
    .ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddSingleton<InterVposOptions>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<InterVposOptions>>().Value);

var app = builder.Build();

await using (var seedScope = app.Services.CreateAsyncScope())
{
    var s = seedScope.ServiceProvider.GetRequiredService<IDocumentSession>();
    await MerchantSeeder.SeedAsync(s, Assembly.GetExecutingAssembly()); // TURU seed
    await CardSeeder.SeedAsync(s);
}

app.MapDefaultEndpoints();
app.MapScalarDocumentation();

app.AddTuruncuPosEndpoints();   // /turuncubank/pos (XML + SHA512 facade)
app.AddOperationEndpoints();    // /api/* (operasyon API — Core generic)
app.AddOpsEndpoints();
app.AddOnboardingEndpoints();
app.AddMerchantPanelEndpoints();

await app.RunAsync();
