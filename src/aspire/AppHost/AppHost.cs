var builder = DistributedApplication.CreateBuilder(args);

// Host portu 5434 — PG (5433) ve varsayılan (5432) Postgres'leriyle çakışmasın (çoklu Aspire dev).
var postgres = builder.AddPostgres("postgres", port: 5434)
    .WithPgAdmin()
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

var rabbit = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

// Her banka kendi veritabanı (ayrı acquirer, ayrı defter).
var mavibankDb = postgres.AddDatabase("mavibankDb");
var saribankDb = postgres.AddDatabase("saribankDb");
var turuncubankDb = postgres.AddDatabase("turuncubankDb");

// MaviBank + SarıBank: InterVpos protokolü (aynı facade lib, ayrı host/DB/config).
builder.AddProject<Projects.MaviBank_Api>("mavibank-api")
    .WithReference(mavibankDb).WithReference(rabbit).WaitFor(mavibankDb).WaitFor(rabbit);

builder.AddProject<Projects.SariBank_Api>("saribank-api")
    .WithReference(saribankDb).WithReference(rabbit).WaitFor(saribankDb).WaitFor(rabbit);

// TuruncuBank: FARKLI protokol (XML + SHA512); Core domain paylaşılır (İlke VII).
builder.AddProject<Projects.TuruncuBank_Api>("turuncubank-api")
    .WithReference(turuncubankDb).WithReference(rabbit).WaitFor(turuncubankDb).WaitFor(rabbit);

builder.Build().Run();
