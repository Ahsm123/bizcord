using System.Reflection;
using System.Text;
using Bizcord.MessageClient;
using Bizcord.Vault;
using Bizcord.WorkspaceApi.Authorization;
using Bizcord.WorkspaceApi.Data;
using Bizcord.WorkspaceApi.Extensions;
using Bizcord.WorkspaceApi.Services;
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
    await builder.Configuration.AddVaultSecretsAsync("workspace-api");
    await builder.Configuration.AddVaultSecretsAsync("shared/jwt");
}

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

// read the key up front, so a missing key fails at startup instead of on the first request
var signingKey = builder.Configuration["Jwt:IssuerSigningKey"]
                 ?? throw new InvalidOperationException("Missing Jwt:IssuerSigningKey");

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

// Every endpoint requires a user token unless it names its own policy, so a new endpoint can't be left open
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.InternalService, p => p.RequireRole(Policies.InternalService))
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
app.UseHttpsRedirection();
app.MapControllers();
app.Run();
return 0;