# REST API

Clients reach the services through the gateway on port 8080. The gateway and WorkspaceApi both validate the
access token, and the acting user is its `sub` (see [decisions.md](decisions.md)).

## Gateway

Same paths as WorkspaceApi. No token or an invalid token returns 401, a missing scope or role returns 403.

| Method | Gateway route                           | Requires                 |
|--------|-----------------------------------------|--------------------------|
| POST   | api/v1/workspaces                       | `workspace.write`        |
| GET    | api/v1/workspaces                       | `workspace.read`         |
| GET    | api/v1/workspaces/{id}                  | `workspace.read`         |
| PUT    | api/v1/workspaces/{id}                  | `workspace.write`        |
| DELETE | api/v1/workspaces/{id}                  | `workspace.write`        |
| POST   | api/v1/workspaces/{id}/channels         | `channel.write`          |
| GET    | api/v1/workspaces/{id}/channels         | `channel.read`           |
| GET    | internal/channels/{id}/members/{userId} | `role: internal-service` |

`POST /faucet` issues a token with the claims you ask for. Development only. User tokens need `role: user`
and a Guid `sub`.

Tests: `newman run tests/Bizcord.Gateway.Postman/Bizcord.Gateway.postman_collection.json` against the
running compose stack.

## WorkspaceApi

All `api/v1` endpoints require `role: user` and the internal endpoint requires `role: internal-service`.
No token returns 401, the wrong role or a non-Guid `sub` returns 403. The internal endpoint returns 200 for a
member and 404 for other.

Only admins can create channels. Creating a channel returns 403 for non-admins, 404 if the workspace doesn't exist
and 409 if the name is already taken in that workspace.
