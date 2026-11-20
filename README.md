# Bizcord

## Context

This repository contains the **Workspace** service, which also covers **Channels**, and the **File** service for
uploads. A small stub stands in for the **Message** service, so the [upload saga](docs/upload-saga.md) can run end to end.

- Workspace is upstream of the other services. Messages and reactions need a workspace and a channel to exist in.
- Channels exist only inside a workspace, so they belong to the same context.
- Workspace owns authorization. It stores memberships and roles (what a user may do in a workspace), not user profile data.

## Folder structure

```
bizcord/
├── src/
│   ├── Bizcord.Gateway/          # Ocelot API gateway, the single entry point for clients
│   ├── Bizcord.WorkspaceApi/     # Workspace service
│   ├── Bizcord.FileApi/          # File service, orchestrates the upload saga
│   ├── Bizcord.MessageStub/      # Stand-in for the Message service in the upload saga
│   ├── Bizcord.Contracts/        # DTOs and events shared with other services
│   └── Bizcord.MessageClient/    # RabbitMQ client wrapper
├── tests/
│   ├── Bizcord.Gateway.Postman/  # Postman collection that tests the gateway against the running stack
│   ├── Bizcord.WorkspaceApi.Tests/
│   ├── Bizcord.FileApi.Tests/
│   ├── Bizcord.MessageStub.Tests/
│   └── Bizcord.MessageClient.Tests/
├── docs/                         # diagrams, bounded contexts, events, REST API, the upload saga and Vault
├── vault/                        # Vault config and the development setup script
├── fluentd/                      # Config for the logging sidecar
├── compose.yaml
└── Bizcord.slnx
```

The repo uses the standard .NET `src/` + `tests/` layout instead of one folder per service. Production code and tests stay
separate, and everything builds from one solution file.

The [Dockerfile](src/Bizcord.WorkspaceApi/Dockerfile) is built from the repo root (see `compose.yaml`) because it needs the
shared projects in `src/`.

## Diagrams

- [C4 Level 2](./docs/c4-level2-container.drawio.svg)

## Services

What each service owns and does not own: [docs/bounded-contexts.md](docs/bounded-contexts.md).

## Interactions

How the services talk to each other:

- [Events](docs/events.md): which events each service publishes and consumes
- [REST API](docs/rest-api.md): endpoints and the `X-User-Id` header

## Running it

The services read their connection strings and the JWT signing key from Vault. The first time on a new machine,
follow [docs/vault.md](docs/vault.md) to set up Vault and `.env`, then run `docker compose up -d`.

## Logs

A Fluentd sidecar collects the stdout of every service: `docker compose logs -f fluentd`.
See [docs/logging.md](docs/logging.md).

## Ports

Only the gateway is reachable for clients. The services themselves have no host port (#72).

| Service      | Host port                |
|--------------|--------------------------|
| gateway      | 8080                     |
| workspace-db | 5433                     |
| file-db      | 5434                     |
| rabbitmq     | 5672, 15672 (management) |
| vault        | 8200 (localhost only)    |


## Collaboration

1. Add an issue e.g.: "update controller method to handle missing ..."
2. Write short list of requirements / why this needs to be changed
3. From terminal: "gh issue list" to see open issue
4. "gh issue edit N --add-assignee @me" where N is the issue number from the list with the #.
5. "gh issue develop N --checkout" to checkout a branch
6. When issue is done:
   - git add .
   - write correct commit message e.g.: "fix: update workspace controller to fix X #N" - tag the issue
   - git push
7. "gh pr create --fill --body "Closes #N"" - important to tag the issue to auto close it with the #
8. "gh pr merge --squash --delete-branch" - so the branch is squash merged and deleted correctly
9. "git switch main && git pull --prune" make sure to switch back and clean up
