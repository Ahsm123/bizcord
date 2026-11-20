using System.Net;
using System.Net.Http.Json;

namespace Bizcord.Vault;

public static class VaultConfigurationExtensions
{
    // Reads the KV v2 secret at secret/<path> and adds its keys to the configuration, e.g. "ConnectionStrings:Database".
    // Any problem stops the service at startup with a message that says what to fix (see docs/vault.md).
    public static async Task AddVaultSecretsAsync(this IConfigurationManager config, string path,
        HttpMessageHandler? handler = null)
    {
        var token = config["VAULT_TOKEN"]
                    ?? throw new InvalidOperationException(
                        "Missing VAULT_TOKEN: set it to the service's scoped Vault token.");
        var address = config["VAULT_ADDR"];

        using var client = new HttpClient(handler ?? new HttpClientHandler()) { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.Add("X-Vault-Token", token);

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync($"{address}/v1/secret/data/{path}");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new InvalidOperationException($"Could not reach Vault at {address}. Is it running?", e);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(response.StatusCode switch
                {
                    HttpStatusCode.Forbidden =>
                        $"Vault denied access to secret/{path}. Check that VAULT_TOKEN is this service's token " +
                        "and that its policy can read the path.",
                    HttpStatusCode.ServiceUnavailable =>
                        "Vault is sealed. Unseal it (see docs/vault.md) and start the service again.",
                    HttpStatusCode.NotFound => $"secret/{path} does not exist in Vault (see docs/vault.md).",
                    _ => $"Vault answered {(int)response.StatusCode} when reading secret/{path}."
                });
            }

            var secret = await response.Content.ReadFromJsonAsync<VaultResponse>();
            config.AddInMemoryCollection(secret!.Data.Data);
        }
    }

    private record VaultResponse(VaultData Data);

    private record VaultData(Dictionary<string, string?> Data);
}
