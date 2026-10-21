
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.AddOpenApiDocumentation();

// SariBank host — kendi şeması (saribank), InterVpos facade. Core domain + InterVpos facade paylaşılan lib.
var bankDb = builder.Configuration.GetConnectionString("saribankDb")!;
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
    opts.IncludeCoreHandlers();
    opts.IncludeInterVposHandlers();
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
    await MerchantSeeder.SeedAsync(s, Assembly.GetExecutingAssembly()); // bu host'un merchant seed'i (MAVI)
    await CardSeeder.SeedAsync(s);                                      // ortak test kartları (Core)
}

app.MapDefaultEndpoints();
app.MapScalarDocumentation();

app.AddInterVposEndpoints();        // /mpi/Default.aspx + /mpi/3DAcs (InterVpos facade)
app.AddOperationEndpoints();        // /api/* (operasyon API)
app.AddOpsEndpoints();              // /ops/* (gün sonu, release-bloke)
app.AddOnboardingEndpoints();       // /onboarding/*
app.AddMerchantPanelEndpoints();    // /panel/*

await app.RunAsync();
