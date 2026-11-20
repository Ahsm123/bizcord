using System.Net.Http.Headers;
using System.Text;
using Bizcord.MessageClient.Clients;
using Bizcord.WorkspaceApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Bizcord.WorkspaceApi.Tests;

// Runs the API in memory with the fake repository and message client,
// so HTTP tests don't need Postgres or RabbitMQ.
// Tokens are real JWTs signed with a test key, so the API's token validation runs as in production.
public class TestApiFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "test-issuer";
    public const string Audience = "workspace-api";
    private const string SigningKey = "test-signing-key-that-is-at-least-32-bytes-long";

    public FakeWorkspaceRepository Repository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:IssuerSigningKey", SigningKey);
        builder.UseSetting("Jwt:ValidIssuer", Issuer);
        builder.UseSetting("Jwt:ValidAudience", Audience);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IWorkspaceRepository>();
            services.AddSingleton<IWorkspaceRepository>(Repository);
            services.RemoveAll<IMessageClient>();
            services.AddSingleton<IMessageClient, FakeMessageClient>();
        });
    }

    // A client that sends a token with the given role on every request
    public HttpClient CreateClientWithRole(string role)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", CreateToken(role));
        return client;
    }

    // A request with this header acts as the given user
    public static AuthenticationHeaderValue UserAuthHeader(Guid userId) =>
        new("Bearer", CreateToken("user", userId.ToString()));

    // GraphQL checks the scope per field, so its tests send the scopes too, separated by spaces
    public static AuthenticationHeaderValue UserAuthHeader(Guid userId, string scope) =>
        new("Bearer", CreateToken("user", userId.ToString(), scope: scope));

    // sub defaults to a random user id, pass a value to test what the API does with a bad one
    public static string CreateToken(string role, string? sub = null, string issuer = Issuer,
        string audience = Audience, string? scope = null)
    {
        var claims = new Dictionary<string, object>
        {
            ["sub"] = sub ?? Guid.NewGuid().ToString(),
            ["role"] = role
        };
        if (scope is not null)
        {
            claims["scope"] = scope;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = claims,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
