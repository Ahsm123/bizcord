# bizcord

**Im working on Workspace, which includes Channels.**

I picked Workspace because the other services depend on it. You cant have messages or reactions
if theres no workspace or channel to put them in. Channels are part of it since they only exist inside a workspace.

Workspace also owns authz, since it keeps track of members and their roles. It doesnt store info about
the members themselves, just what theyre allowed to do inside the workspace.

## Ports

| Service | Host-port |
|---|---|
| workspace-api | 5001 |
| workspace-db | 5433 |
| rabbitmq | 5672, 15672 (management) |
