var builder = DistributedApplication.CreateBuilder(args);

// Host portu 5434 — PG (5433) ve varsayılan (5432) Postgres'leriyle çakışmasın (çoklu Aspire dev).
var postgres = builder.AddPostgres("postgres", port: 5434)
    .WithPgAdmin()
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var rabbit = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

var bankDb = postgres.AddDatabase("bankDb");

// İlk banka facade (MaviBank / InterVpos). İleride SarıBank/TuruncuBank aynı repoda ek facade.
builder.AddProject<Projects.MaviBank_Api>("mavibank-api")
    .WithReference(bankDb)
    .WithReference(rabbit)
    .WaitFor(bankDb)
    .WaitFor(rabbit);

builder.Build().Run();
