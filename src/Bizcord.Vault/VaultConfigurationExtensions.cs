using System.Net.Http.Json;

namespace Bizcord.Vault;

public static class VaultConfigurationExtensions
{
    public static async Task AddVaultSecretsAsync(this IConfigurationManager config, string path,
        HttpMessageHandler? handler = null)
    {
        var token = config["VAULT_TOKEN"]
                    ?? throw new InvalidOperationException(
                        "Missing VAULT_TOKEN: set it to the service's scoped Vault token.");

        using var client = new HttpClient(handler ?? new HttpClientHandler()) { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.Add("X-Vault-Token", token);

        var secret = await client.GetFromJsonAsync<VaultResponse>($"{config["VAULT_ADDR"]}/v1/secret/data/{path}");
        config.AddInMemoryCollection(secret!.Data.Data);
    }

    private record VaultResponse(VaultData Data);

    private record VaultData(Dictionary<string, string?> Data);
}