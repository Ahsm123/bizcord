using System.Reflection;
using System.Text;
using Bizcord.FileApi.Authorization;
using Bizcord.FileApi.Data;
using Bizcord.FileApi.Extensions;
using Bizcord.FileApi.Services;
using Bizcord.MessageClient;
using Bizcord.Vault;
using DbUp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Secrets
if (builder.Configuration["VAULT_TOKEN"] is not null)
{
    await builder.Configuration.AddVaultSecretsAsync("file-api");
    await builder.Configuration.AddVaultSecretsAsync("shared/jwt");
}

// Data
var connectionString = builder.Configuration.GetConnectionString("Database")
                       ?? throw new InvalidOperationException("Missing ConnectionStrings:Database");

// The migrator only needs the database, so it runs before the services that need RabbitMQ
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
builder.Services.AddScoped<IUploadRepository, UploadRepository>();

// Services
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IUploadService, UploadService>();

// The timeout job for sagas where a service never answered
builder.Services.Configure<UploadSagaOptions>(builder.Configuration.GetSection("UploadSaga"));
builder.Services.AddScoped<StalledUploads>();
builder.Services.AddHostedService<StalledUploadsWorker>();

// Messaging: registers every IMessageHandler<T> in this assembly and subscribes a worker for each message type
builder.Services.AddMessaging(builder.Configuration, Assembly.GetExecutingAssembly());

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddRequestTimeouts(options =>
    options.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(2) });

// Read the key up front, so a missing key fails at startup instead of on the first request
var signingKey = builder.Configuration["Jwt:IssuerSigningKey"]
                 ?? throw new InvalidOperationException("Missing Jwt:IssuerSigningKey");

// Checks the token again even though the gateway did, so a call inside the compose network can't skip it (#72)
builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwtOptions =>
    {
        jwtOptions.MapInboundClaims = false;
        jwtOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidAudience = builder.Configuration["Jwt:ValidAudience"],
            ValidIssuer = builder.Configuration["Jwt:ValidIssuer"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
    });

// Every endpoint requires a user token, so a new endpoint can't be left open
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireRole(Policies.User)
        .RequireAssertion(ctx => ctx.User.HasUserId())
        .Build());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRequestTimeouts();
app.MapControllers();
app.Run();
return 0;
