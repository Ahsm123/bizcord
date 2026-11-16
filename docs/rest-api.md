# REST API

Clients reach the services through the gateway on port 8080. It validates the access token and sets the
`X-User-Id` header from the token's `sub` claim (see [bounded-contexts.md](bounded-contexts.md)).

## Gateway

Same paths as WorkspaceApi. No token or an invalid token returns 401, a token without the scope returns 403.

| Method | Gateway route                   | Scope             |
|--------|---------------------------------|-------------------|
| POST   | api/v1/workspaces               | `workspace.write` |
| GET    | api/v1/workspaces               | `workspace.read`  |
| GET    | api/v1/workspaces/{id}          | `workspace.read`  |
| PUT    | api/v1/workspaces/{id}          | `workspace.write` |
| DELETE | api/v1/workspaces/{id}          | `workspace.write` |
| POST   | api/v1/workspaces/{id}/channels | `channel.write`   |
| GET    | api/v1/workspaces/{id}/channels | `channel.read`    |

`POST /faucet` issues a token with the claims you ask for. Development only. `sub` has to be a Guid.

Tests: `newman run tests/Bizcord.Gateway.Postman/Bizcord.Gateway.postman_collection.json` against the
running compose stack.

## WorkspaceApi

A missing or empty `X-User-Id` returns 400.

| Method | Endpoint                        | Header      |
|--------|---------------------------------|-------------|
| POST   | api/v1/workspaces               | `X-User-Id` |
| GET    | api/v1/workspaces               | `X-User-Id` |
| GET    | api/v1/workspaces/{id}          |             |
| PUT    | api/v1/workspaces/{id}          | `X-User-Id` |
| DELETE | api/v1/workspaces/{id}          | `X-User-Id` |
| POST   | api/v1/workspaces/{id}/channels | `X-User-Id` |
| GET    | api/v1/workspaces/{id}/channels |             |

Only admins can create channels. Creating a channel returns 403 for non-admins, 404 if the workspace doesn't exist
and 409 if the name is already taken in that workspace.
