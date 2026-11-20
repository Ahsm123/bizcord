# Vault

Connection strings and the JWT signing key are not in `compose.yaml` or appsettings. Each service reads them from
HashiCorp Vault when it starts ([VaultConfigurationExtensions](../src/Bizcord.Vault/VaultConfigurationExtensions.cs)),
using a token that can only read that service's own secrets.

## Secrets

KV v2 engine mounted at `secret/`. The keys are configuration keys, so they land in `IConfiguration` as they are.

| Path                   | Keys                                                       | Read by                           |
|------------------------|------------------------------------------------------------|-----------------------------------|
| `secret/workspace-api` | `ConnectionStrings:Database`, `ConnectionStrings:Messaging` | workspace-api, workspace-migrator |
| `secret/shared/jwt`    | `Jwt:IssuerSigningKey`                                     | workspace-api, gateway            |

Every service has its own policy that only allows `read` on its paths, and its own token in `.env`.

## First time setup

Vault keeps its data in the `vault-data` volume, so this is only needed once per machine.

1. Start Vault:

   ```bash
   docker compose up -d vault
   ```

2. Initialize it. Save the 3 unseal keys and the root token somewhere safe outside the repo (e.g. a password manager):

   ```bash
   docker compose exec vault vault operator init -key-shares=3 -key-threshold=2
   ```

3. Unseal it with 2 of the 3 keys (run it twice):

   ```bash
   docker compose exec vault vault operator unseal
   ```

4. Create the secrets, policies and service tokens. Replace `<root token>` with the root token from step 2:

   ```bash
   docker compose exec -T -e VAULT_TOKEN=<root token> vault sh -s < vault/dev-setup.sh
   ```

5. Copy the `..._VAULT_TOKEN=` lines it prints into `.env` in the repo root. Git ignores `.env`.

6. Start everything:

   ```bash
   docker compose up -d
   ```

## After a restart

Vault starts sealed, and the services wait for its healthcheck. Unseal it with 2 keys (step 3), then run
`docker compose up -d` again.

The service tokens expire after 768 hours. Run step 4 again for new ones and replace the lines in `.env`.

## When it goes wrong

A service stops at startup with a message that says what is wrong:

| Message                         | Cause                                                            |
|---------------------------------|------------------------------------------------------------------|
| `Missing VAULT_TOKEN`           | The token is missing in `.env`                                   |
| `Vault denied access to ...`    | Wrong or expired token, or the policy doesn't allow the path (403) |
| `Vault is sealed`               | Vault was restarted and needs to be unsealed (503)               |
| `... does not exist in Vault`   | `vault/dev-setup.sh` hasn't been run                              |
| `Could not reach Vault at ...`  | The vault container isn't running                                 |

Running a service with `dotnet run` outside Docker doesn't use Vault: without `VAULT_TOKEN` it reads the connection
strings from `appsettings.Development.json`. The signing key isn't there, so pass it in yourself, e.g. with the
`Jwt__IssuerSigningKey` environment variable.
