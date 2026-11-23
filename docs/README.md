# Compulsory Assignment #2: how it is built and how to verify it

This page maps each task from weeks 43–47 to the code, the design decisions behind it, and how to check that it works.
The decisions themselves are in [decisions.md](decisions.md) (in Danish) and in the topic pages linked below.

## Run it from a fresh clone

You need Docker. Everything runs in compose, and only the gateway is reachable from outside, on
`http://localhost:8080`.

1. Set up Vault and `.env` once per machine: [vault.md](vault.md#first-time-setup).
2. Start everything:

   ```bash
   docker compose up -d --build
   ```

3. Check the whole system through the gateway with the Postman collection (35 requests, 36 assertions):

   ```bash
   npx newman run tests/Bizcord.Gateway.Postman/Bizcord.Gateway.postman_collection.json
   ```

4. Run the unit, API and integration tests. The integration tests use the Postgres and RabbitMQ from compose, which
   step 2 started and migrated. Stop the services first, because they share the databases and queues with the tests
   and would otherwise take the tests' messages:

   ```bash
   docker compose stop workspace-api file-api message-stub
   ```

   ```bash
   dotnet test Bizcord.slnx
   ```

The services: the gateway, WorkspaceApi (workspaces, channels, members and the storage quota), FileApi (uploads,
orchestrates the upload saga), a Message stub, Postgres per service, RabbitMQ, Vault and a Fluentd sidecar.
See [bounded-contexts.md](bounded-contexts.md) for what each one owns.

## Task 1: API gateway (week 43)

**Built:** An Ocelot gateway ([src/Bizcord.Gateway](../src/Bizcord.Gateway)) with routes to WorkspaceApi and FileApi
([ocelot.workspace.json](../src/Bizcord.Gateway/Configuration/ocelot.workspace.json),
[ocelot.file.json](../src/Bizcord.Gateway/Configuration/ocelot.file.json)).

- Every route needs a valid JWT (`Bearer`), and each route needs its own scope, e.g. `workspace.write` to create a
  workspace or `file.write` to upload. The table is in [rest-api.md](rest-api.md#gateway).
- In development, `POST /faucet` issues tokens with the claims you ask for, as a stand-in for an identity provider.
- `Location` headers from the services are rewritten to the gateway's address, so a client never sees an internal host.

**Decision, and where we differ from the task:** The task suggests forwarding claims as headers, e.g. `X-UserId` from
`sub`. We don't. The gateway removes any `X-User-Id` a client sends, and each service validates the token again and
reads the user from `sub`. A header can be set by anyone who can reach the service, the token can't be faked. See
[decisions.md](decisions.md#workspaceapi-tjekker-selv-tokens).

**Verify:**

- Postman folders *Authentication and authorization* and *Routes*: 401 without or with an invalid token, 403 without
  the scope, every route reachable, and a spoofed `X-User-Id` is ignored.
- Tests: `UserIdClaimTests`, `WorkspaceEndpointTests`, `ChannelEndpointsTests`, `UploadEndpointsTests`.

## Task 2: Reliability (week 44)

**Failure points:** in the folder of each service, as the task asks:
[WorkspaceApi](../src/Bizcord.WorkspaceApi/README.md) and [FileApi](../src/Bizcord.FileApi/README.md). Each one says
what fails, what happens then, and whether it is handled or still open.

**Measures:**

| Failure point                                           | Pattern                        | Where                                                                                     |
|---------------------------------------------------------|--------------------------------|-------------------------------------------------------------------------------------------|
| A slow database hangs requests                          | Timeout: 504 after 2 seconds   | `AddRequestTimeouts` in both APIs' `Program.cs` (#65)                                       |
| A short outage fails a message                          | Retry with backoff (1, 2, 4 s) | [MessageHandlerWorker](../src/Bizcord.MessageClient/Workers/MessageHandlerWorker.cs) (#65) |
| An event is lost if RabbitMQ is down after the update   | Transactional outbox           | [Outbox](../src/Bizcord.WorkspaceApi/Outbox) (#66), [decision](decisions.md#outbox-til-channelactivityupdatedevent-66) |
| A retried `POST /workspaces` creates two workspaces     | Idempotency key                | `Idempotency-Key` header (#67), [decision](decisions.md#idempotent-oprettelse-af-workspaces-67) |
| Two requests overwrite each other's change              | Optimistic concurrency, 409    | `version` on workspaces (#68), [decision](decisions.md#samtidige-ændringer-af-et-workspace-68) |
| A service in the saga never answers                     | Timeout job and compensation   | [StalledUploads](../src/Bizcord.FileApi/Services/StalledUploads.cs) (#82)                    |

**Verify:**

- Tests: `RequestTimeoutTests`, `OutboxTests` (an event saved while RabbitMQ is down goes out when it is back),
  `IdempotencyKeyTests` and `IdempotencyKeyRepositoryTests` (five parallel requests with one key create one workspace),
  `WorkspaceConcurrencyTests` (parallel changes: none is lost, the rest get 409), `StalledUploadsTests`.
- By hand: `docker compose stop workspace-api`, upload a file through the gateway, and after 30 seconds
  `GET api/v1/uploads/{id}` says `Rejected`. Start it again, and the quota is not reserved
  ([upload-saga.md](upload-saga.md#try-it)).

## Task 3: Security (week 45)

**East/west:** Before a message is posted, the Message service asks Workspace whether the user is a member of the
channel, through an internal endpoint: `GET internal/channels/{channelId}/members/{userId}`. Only the endpoint is built
here, since Message is a stub; the Postman collection calls it with a service token. It needs a token with
`role: internal-service`, checked by the gateway (`RouteClaimsRequirement`) and again by WorkspaceApi. User tokens need `role: user`. The services have no host
ports, and they validate every token themselves, so a call inside the compose network can't skip the check either.
See [decisions.md](decisions.md#eastwest-security-i-workspaceapi-72).

**Vault:** The connection strings and the JWT signing key live in Vault (KV v2). Each service has its own token and a
policy that only lets it read its own secrets, and reads them at startup
([Bizcord.Vault](../src/Bizcord.Vault/VaultConfigurationExtensions.cs)). How to set it up and what each secret is:
[vault.md](vault.md).

**Verify:**

- Postman folder *Internal endpoints*: 401 without a token, 403 with a user token, through with a service token.
- Tests: `InternalChannelsControllerTest`, `VaultConfigurationExtensionsTests`.
- Vault by hand:
  - `docker compose restart vault`, then `docker compose restart workspace-api`, and `docker compose logs workspace-api`
    shows *Vault is sealed*. Unseal it and run `docker compose up -d` ([vault.md](vault.md#after-a-restart)).
  - Put a wrong `WORKSPACE_API_VAULT_TOKEN` in `.env`, run `docker compose up -d workspace-api`, and the log shows
    *Vault denied access*.

## Task 4: Data management, saga (week 46)

**Scenario:** *A user sends a file that turns out to be too large.* The upload is checked against the workspace's
storage quota before the message with the file is posted, so nobody sees a file that gets rejected.

**Built:** Orchestration, with FileApi as the orchestrator. Every step, compensation and failure path, with diagrams
and a table of started by, success, failure and compensation: [upload-saga.md](upload-saga.md). Why orchestration,
why FileApi, and where the saga state lives: [decisions.md](decisions.md#saga-upload-af-en-fil-der-er-for-stor-til-kvoten).

- Steps: FileApi stores the upload (`Pending`) → WorkspaceApi reserves the quota → Message posts the message (the
  pivot) → `Confirmed`.
- Compensation: if Message fails, the quota is released and the upload is `Rejected`. Before the pivot a stalled saga
  is rolled back, after it the saga is pushed forward.
- Every message carries the `SagaId`, and every step is idempotent on it.
- Built on our own MessageClient, as the task says. No saga framework.

**Verify:**

- Postman folder *Uploads and the saga*: an upload that fits ends `Confirmed`, one bigger than the quota ends
  `Rejected`.
- The other paths by hand, with the Message stub's `MESSAGE_STUB_BEHAVIOR` (`Fail`, `NoReply`):
  [upload-saga.md](upload-saga.md#try-it).
- Tests: `UploadSagaOrchestratorTests`, `StalledUploadsTests`, `UploadRepositoryTests`, `QuotaServiceTests`,
  `QuotaRepositoryTests` (two parallel reservations can't both get the last space), `PostMessageHandlerTests`.

## Task 5: Maintainability and decoupling (week 47)

**Logging sidecar:** The services only log to stdout. A Fluentd container tails Docker's log files, keeps only the
Bizcord services (tagged with their container name), and prints them, standing in for a central log system. No service
has logging code for it. See [logging.md](logging.md). Forwarding to a real log store is the optional part, and isn't
done (#84).

**GraphQL:** WorkspaceApi has a GraphQL endpoint on `/graphql` next to the REST API, built with Hot Chocolate. The
resolvers only call `IWorkspaceService`, the same service layer as the REST controllers, and the GraphQL types are
separate from the domain models. The gateway can't check scopes per GraphQL operation, so WorkspaceApi checks them per
field. See [graphql.md](graphql.md).

**Verify:**

- Logs: `docker compose logs -f fluentd` while you call the gateway. Each line has a `container` field.
- Postman folder *GraphQL*: a query with channels and members in one request, a mutation, a mutation refused for a
  read-only token, and 403 at the gateway without a GraphQL scope.
- Tests: `GraphQLTests`.

## More

- [events.md](events.md): every event and command on RabbitMQ
- [rest-api.md](rest-api.md): every route, through the gateway and on each service
- [c4-level2-container.drawio.svg](c4-level2-container.drawio.svg): the containers
