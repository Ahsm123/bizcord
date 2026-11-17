using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Bizcord.Vault.Tests;

public class VaultConfigurationExtensionsTests
{
    private const string SecretJson =
        """{ "data": { "data": { "ConnectionStrings:Database": "Host=db" } } }""";

    [Fact]
    public async Task Secret_values_land_in_configuration_under_their_vault_key()
    {
        var config = ConfigWith(token: "app-token");
        var handler = new FakeVaultHandler(HttpStatusCode.OK, SecretJson);

        await config.AddVaultSecretsAsync("workspace-api", handler);

        Assert.Equal("Host=db", config["ConnectionStrings:Database"]);
        Assert.Equal("http://vault:8200/v1/secret/data/workspace-api", handler.Request!.RequestUri!.ToString());
        Assert.Equal("app-token", handler.Request.Headers.GetValues("X-Vault-Token").Single());
    }

    [Fact]
    public async Task Missing_token_fails_before_calling_vault()
    {
        var config = ConfigWith(token: null);
        var handler = new FakeVaultHandler(HttpStatusCode.OK, SecretJson);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => config.AddVaultSecretsAsync("workspace-api", handler));

        Assert.Contains("VAULT_TOKEN", error.Message);
        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Denied_access_fails_startup()
    {
        var config = ConfigWith(token: "wrong-token");
        var handler = new FakeVaultHandler(HttpStatusCode.Forbidden, """{ "errors": ["permission denied"] }""");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => config.AddVaultSecretsAsync("workspace-api", handler));
    }

    private static ConfigurationManager ConfigWith(string? token)
    {
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VAULT_ADDR"] = "http://vault:8200",
            ["VAULT_TOKEN"] = token
        });
        return config;
    }

    private sealed class FakeVaultHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
