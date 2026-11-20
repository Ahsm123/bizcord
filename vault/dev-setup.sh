#!/bin/sh
# Development setup for the Vault in compose.yaml: the secrets, a read-only policy per service and a token per service.
# Run it after init and unseal (see docs/vault.md):
#   docker compose exec -T -e VAULT_TOKEN=<root token> vault sh -s < vault/dev-setup.sh
# It prints the lines for .env. Running it again replaces the connection strings and creates new tokens,
# but keeps the JWT signing key, so tokens from the faucet stay valid.
set -e
export VAULT_ADDR=http://127.0.0.1:8200

vault secrets list | grep -q '^secret/' || vault secrets enable -path=secret kv-v2 >/dev/null

# Development values: the same credentials the Postgres and RabbitMQ containers in compose.yaml start with
vault kv put secret/workspace-api \
  'ConnectionStrings:Database=Host=workspace-db;Port=5432;Database=workspace;Username=postgres;Password=postgres' \
  'ConnectionStrings:Messaging=host=rabbitmq;username=kalo;password=kalo' >/dev/null

vault kv put secret/message-stub   'ConnectionStrings:Messaging=host=rabbitmq;username=kalo;password=kalo' >/dev/null

# Shared by every service that issues or checks tokens
if ! vault kv get secret/shared/jwt >/dev/null 2>&1; then
  vault kv put secret/shared/jwt "Jwt:IssuerSigningKey=$(head -c 48 /dev/urandom | base64 | tr -d '\n')" >/dev/null
fi

vault policy write workspace-api - >/dev/null <<'EOF'
path "secret/data/workspace-api" { capabilities = ["read"] }
path "secret/data/shared/jwt" { capabilities = ["read"] }
EOF

vault policy write message-stub - >/dev/null <<'EOF'
path "secret/data/message-stub" { capabilities = ["read"] }
EOF

vault policy write gateway - >/dev/null <<'EOF'
path "secret/data/shared/jwt" { capabilities = ["read"] }
EOF

# One token per service, each only allowed to read its own paths. 768h is Vault's default max TTL.
token() { vault token create -policy="$1" -no-default-policy -ttl=768h -field=token; }

echo "# Paste into .env (git ignores it)"
echo "WORKSPACE_API_VAULT_TOKEN=$(token workspace-api)"
echo "GATEWAY_VAULT_TOKEN=$(token gateway)"
echo "MESSAGE_STUB_VAULT_TOKEN=$(token message-stub)"
