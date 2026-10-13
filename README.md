# Bizcord

## Chosen context

**Im working on Workspace, which includes Channels.**

I picked Workspace because the other services depend on it. You cant have messages or reactions
if theres no workspace or channel to put them in. Channels are part of it since they only exist inside a workspace.

Workspace also owns authz, since it keeps track of members and their roles. It doesnt store info about
the members themselves, just what theyre allowed to do inside the workspace.

## Folder structure

```
bizcord/
├── src/
│   ├── Bizcord.WorkspaceApi/     # the Workspace service
│   ├── Bizcord.Contracts/        # DTOs and events shared with other services
│   └── Bizcord.MessageClient/    # RabbitMQ client wrapper
├── tests/
│   └── Bizcord.WorkspaceApi.Tests/
├── docs/                         # diagrams and bounded contexts
├── compose.yaml
└── Bizcord.slnx
```

I use the standard .NET `src/` + `tests/` layout instead of one folder per service, so production code and
tests are kept apart and everything builds from one solution file.

The Dockerfile is at [src/Bizcord.WorkspaceApi/Dockerfile](src/Bizcord.WorkspaceApi/Dockerfile). It's built from the
repo root (see `compose.yaml`), since it needs the shared projects in `src/` too.

## Diagrams

- [C4 Level 2](./docs/c4-level2-container.drawio.svg)

## Services

What each service owns and doesnt own: [docs/bounded-contexts.md](docs/bounded-contexts.md).

## Events

| Event                       | Publisher    | Consumers                                                               | Payload                                                 |
|-----------------------------|--------------|-------------------------------------------------------------------------|---------------------------------------------------------|
| MessagePostedEvent          | MessageApi   | `WorkspaceApi`, `Realtime Gateway (planned)`, `Notifications (planned)` | `MessageId` `ChannelId` `AuthorId` `Content` `PostedAt` |
| ChannelActivityUpdatedEvent | WorkspaceApi | `Realtime Gateway (planned)`                                            | `MessageId` `ChannelId` `LastActivityAt`                |        

## REST overview

| Method | Endpoint               | Header      |
|--------|------------------------|-------------|
| POST   | api/v1/workspaces      | `X-User-Id` |
| GET    | api/v1/workspaces      | `X-User-Id` |
| GET    | api/v1/workspaces/{id} |             |
| PUT    | api/v1/workspaces/{id} | `X-User-Id` |
| DELETE | api/v1/workspaces/{id} | `X-User-Id` |

## Ports

| Service       | Host-port                |
|---------------|--------------------------|
| workspace-api | 5001                     |
| workspace-db  | 5433                     |
| rabbitmq      | 5672, 15672 (management) |


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
