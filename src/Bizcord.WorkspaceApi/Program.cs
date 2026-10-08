using System.Reflection;
using Bizcord.MessageClient;
using Bizcord.MessageClient.Handlers;
using Bizcord.Contracts.Events;
using Bizcord.WorkspaceApi.Handlers;
using Bizcord.WorkspaceApi.Data;
using Bizcord.WorkspaceApi.Services;
using Bizcord.WorkspaceApi.Workers;
using DbUp;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Data
var connectionString = builder.Configuration.GetConnectionString("Database")
                       ?? throw new InvalidOperationException("Missing ConnectionStrings:Database");
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();

// Services
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();

// Messaging
builder.Services.AddScoped<IMessageHandler<MessagePostedEvent>, MessagePostedHandler>();
builder.Services.AddMessaging(builder.Configuration);

// Workers
builder.Services.AddHostedService<MessagePostedWorker>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
return 0;
