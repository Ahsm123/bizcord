# REST API

Clients reach the services through the BFF. It validates the access token and passes the user id on in the
`X-User-Id` header (see [bounded-contexts.md](bounded-contexts.md)). A missing or empty `X-User-Id` returns 400.

## WorkspaceApi

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
