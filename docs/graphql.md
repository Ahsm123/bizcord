# GraphQL

WorkspaceApi has a GraphQL endpoint on `/graphql` next to its REST API. It is a presentation layer for frontend
clients (BFF style): a client asks for exactly the data a screen needs, e.g. a workspace with its channels and members in
one request, where REST needs two calls.

The resolvers ([WorkspaceQueries](../src/Bizcord.WorkspaceApi/GraphQL/WorkspaceQueries.cs) and
[WorkspaceMutations](../src/Bizcord.WorkspaceApi/GraphQL/WorkspaceMutations.cs)) only call `IWorkspaceService`, the same
service layer the REST controllers use. So there is no business logic in the GraphQL layer, and both APIs follow the
same rules. The GraphQL types are their own records and not the domain models, so the domain can change without
changing the schema.

Built with [Hot Chocolate](https://chillicream.com/docs/hotchocolate).

## Schema

```graphql
type Query {
  myWorkspaces: [WorkspaceSummary!]!
  workspace(id: UUID!): Workspace!
}

type Mutation {
  createWorkspace(name: String!): WorkspaceSummary!
  createChannel(workspaceId: UUID!, name: String!): Channel!
}

type WorkspaceSummary {
  id: UUID!
  name: String!
}

type Workspace {
  id: UUID!
  name: String!
  channels: [Channel!]!
  members: [Member!]!
}

type Channel {
  id: UUID!
  name: String!
}

type Member {
  userId: UUID!
  role: MemberRole!
}

enum MemberRole {
  USER
  ADMIN
}
```

The full schema, including the built-in types: `GET /graphql?sdl` with a token.

## Access

The endpoint needs a user token (`role: user` and a Guid `sub`), like the REST API. The caller is the token's `sub`.

GraphQL has one endpoint for both reads and writes, so the gateway can't check the scope per operation the way it does
per REST route. The gateway only lets a token through when it has at least one of the scopes below, and WorkspaceApi
checks the exact scope per field:

| Field             | Scope             |
|-------------------|-------------------|
| `myWorkspaces`    | `workspace.read`  |
| `workspace`       | `workspace.read`  |
| `createWorkspace` | `workspace.write` |
| `createChannel`   | `channel.write`   |

A field without its scope returns the error code `AUTH_NOT_AUTHORIZED`.

## Example

Get a token from the faucet with the scopes you need, separated by spaces:

```bash
curl -s -X POST http://localhost:8080/faucet -H "Content-Type: application/json" -d '{"claims":{"sub":"3f2b8c1e-7a4d-4e9b-9c21-5d6e7f8a9b0c","role":"user","scope":"workspace.read workspace.write channel.write"}}'
```

Then send a query with the token's `value`:

```bash
curl -s -X POST http://localhost:8080/graphql -H "Authorization: Bearer <token>" -H "Content-Type: application/json" -d '{"query":"query($id: UUID!) { workspace(id: $id) { name channels { name } members { userId role } } }","variables":{"id":"<workspace id>"}}'
```

```json
{
  "data": {
    "workspace": {
      "name": "team",
      "channels": [ { "name": "general" } ],
      "members": [ { "userId": "3f2b8c1e-7a4d-4e9b-9c21-5d6e7f8a9b0c", "role": "ADMIN" } ]
    }
  }
}
```

## Errors

A failed operation returns a GraphQL error instead of ProblemDetails. `code` is the same as the REST `title`, and
`status` is the status code the REST endpoint would have returned:

```json
{
  "errors": [
    {
      "message": "Workspace was not found",
      "path": [ "workspace" ],
      "extensions": { "code": "Workspace.NotFound", "status": 404 }
    }
  ]
}
```

An invalid name (empty or more than 100 characters) returns `Workspace.Validation` with status 400.
