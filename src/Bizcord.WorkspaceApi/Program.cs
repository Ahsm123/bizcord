using System.Reflection;
using Bizcord.MessageClient;
using Bizcord.WorkspaceApi.Data;
using Bizcord.WorkspaceApi.Services;
using DbUp;
using Microsoft.AspNetCore.Http.Timeouts;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Data
var connectionString = builder.Configuration.GetConnectionString("Database")
                       ?? throw new InvalidOperationException("Missing ConnectionStrings:Database");

// The migrator only needs the database, so it runs before any services that need other config, like RabbitMQ
if (args.Contains("migrate"))
{
    var upgrade = DeployChanges.To
        .PostgresqlDatabase(connectionString)
        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
        .LogToConsole()
        .Build()
        .PerformUpgrade();

    return upgrade.Successful ? 0 : 1;
}

var csb = new NpgsqlConnectionStringBuilder(connectionString) { CancellationTimeout = -1 };
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(csb.ConnectionString));
builder.Services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();

// Services
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();

// Messaging: registers every IMessageHandler<T> in this assembly and subscribes a worker for each message type
builder.Services.AddMessaging(builder.Configuration, Assembly.GetExecutingAssembly());

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddRequestTimeouts(options =>
    options.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(2) });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseRequestTimeouts();
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
return 0;