
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
});

builder.Services.AddAllDependencies();

var app = builder.Build();
app.MapDefaultEndpoints();
app.MapScalarDocumentation();

// Faz 1: InterVpos ödeme yüzü (POST /mpi/Default.aspx, TxnType=Auth) burada map'lenecek (spec doldurur).

await app.RunAsync();
