using System.Net.Http.Headers;
using System.Text;
using Bizcord.FileApi.Data;
using Bizcord.MessageClient.Clients;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Bizcord.FileApi.Tests;

// Runs the API in memory with the fake repository and message client, so HTTP tests need no Postgres or RabbitMQ.
// Tokens are real JWTs signed with a test key, so the token validation runs as in production.
public class TestApiFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "test-issuer";
    public const string Audience = "file-api";
    private const string SigningKey = "test-signing-key-that-is-at-least-32-bytes-long";

    public FakeUploadRepository Repository { get; } = new();
    public FakeMessageClient MessageClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:IssuerSigningKey", SigningKey);
        builder.UseSetting("Jwt:ValidIssuer", Issuer);
        builder.UseSetting("Jwt:ValidAudience", Audience);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUploadRepository>();
            services.AddSingleton<IUploadRepository>(Repository);
            services.RemoveAll<IMessageClient>();
            services.AddSingleton<IMessageClient>(MessageClient);
        });
    }

    public static AuthenticationHeaderValue UserAuthHeader(Guid userId) =>
        new("Bearer", CreateToken("user", userId.ToString()));

    public static string CreateToken(string role, string? sub = null, string audience = Audience)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Expires = DateTime.UtcNow.AddMinutes(5),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = sub ?? Guid.NewGuid().ToString(),
                ["role"] = role
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
