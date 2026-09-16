using System.Reflection;
using DbUp;

var builder = WebApplication.CreateBuilder(args);

if (args.Contains("migrate"))
{
    var connectionString = builder.Configuration.GetConnectionString("Workspace")!;

    var upgrade = DeployChanges.To
        .PostgresqlDatabase(connectionString)
        .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
        .LogToConsole()
        .Build()
        .PerformUpgrade();

    return upgrade.Successful ? 0 : 1;
}

// Add services to the container.
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();
return 0;

