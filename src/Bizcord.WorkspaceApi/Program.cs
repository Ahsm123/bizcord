using System.Reflection;
using Bizcord.MessageClient;
using Bizcord.MessageClient.Handlers;
using Bizcord.Shared.Events;
using Bizcord.WorkspaceApi.Handlers;
using Bizcord.WorkspaceApi.Data;
using Bizcord.WorkspaceApi.Services;
using Bizcord.WorkspaceApi.Workers;
using DbUp;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Database");

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

// Add services to the container.
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
builder.Services.AddMessaging(builder.Configuration);
builder.Services.AddScoped<IMessageHandler<MessagePostedEvent>, MessagePostedHandler>();
builder.Services.AddHostedService<MessagePostedWorker>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
return 0;

public partial class Program;
