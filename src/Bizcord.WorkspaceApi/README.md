# Failure points

Week 44, Task 01: where WorkspaceApi can fail, what happens then, and what we do about it.
**Handled** means there is a measure and a test, **Open** means we know about it and haven't fixed it.

| Dependency                      | Direction | Type  |
|---------------------------------|-----------|-------|
| Postgres                        | outgoing  | sync  |
| RabbitMQ                        | outgoing  | async |
| Vault                           | outgoing  | sync, at startup |
| Gateway (Ocelot)                | incoming  | sync  |
| Message, via MessagePostedEvent | incoming  | async |
| FileApi, via ReserveQuota and ReleaseQuota | incoming | async |

## Postgres

- **Handled (#65):** A slow database could hang requests and use up the connection pool. Every HTTP request,
  including GraphQL, now fails after 2 seconds with 504.
- **Handled (#65):** If Postgres is down for a short while, a message handler retries after 1, 2 and 4 seconds before
  it gives up.
- **Open:** If Postgres is down for longer, the message ends in EasyNetQ's error queue, so that channel activity or
  quota command is never handled. Nothing moves it back.
- **Open:** If Postgres is down, every HTTP request fails with 500 or 504. Nothing retries, the client has to.
- **Handled (#68):** Two requests that change the same workspace at the same time used to overwrite each other. Now the
  workspace has a version, and the late one gets 409 `Workspace.ConcurrentChange` instead of losing a member or channel.

## RabbitMQ

- **Handled (#66):** `ChannelActivityUpdatedEvent` used to be published after the database update, so if RabbitMQ was
  down right then, the event was lost. Now it is saved in an outbox in the same transaction and published when
  RabbitMQ has confirmed it. Consumers can get it twice and dedupe on `EventId`.
- **Open, tested 23 November:** If RabbitMQ is down when WorkspaceApi starts, HTTP works at first, but within a minute
  subscribing to a queue times out, the message worker fails, and .NET stops the whole service, HTTP included. It
  doesn't come back when RabbitMQ does. In compose this is softened by `depends_on: rabbitmq: service_healthy`, but there
  is no restart policy. The fix is for the worker to keep retrying the subscription (#103).
- **Not tested:** what happens when RabbitMQ goes down while WorkspaceApi is running. EasyNetQ is meant to reconnect.

## Vault

- **Handled (#73):** If Vault is sealed, unreachable or the token is wrong, WorkspaceApi stops at startup with a message
  that says what to fix ([docs/vault.md](../../docs/vault.md)). It only reads Vault at startup, so Vault going down later
  changes nothing.
- **Open:** The service tokens expire after 768 hours, and then the service can't start until the tokens in `.env` are
  renewed.

## Gateway (Ocelot)

- **Handled (#67):** If the gateway or a client retries `POST /workspaces`, a second workspace used to be created. A
  client that sends an `Idempotency-Key` now gets the first workspace back.
- **Open:** The header is optional, so a client without it can still create two workspaces.
- **Open:** WorkspaceApi doesn't limit incoming requests, so a spike or many retries can overload it and Postgres.

## Message, via MessagePostedEvent

- A burst of MessagePostedEvents only makes the queue longer. The handler takes them at its own pace, so channel
  activity is late but not lost.
- A repeated or late MessagePostedEvent has no effect, because the update only runs when `PostedAt` is newer than the
  stored activity.

## FileApi, via ReserveQuota and ReleaseQuota

- **Handled (#78):** Two uploads at the same time can't both use the last free space, because the reservation is one
  atomic SQL update.
- **Handled (#78):** A repeated `ReserveQuota` or `ReleaseQuota` for the same saga changes nothing the second time.
- **Handled (#82):** A `ReleaseQuota` that arrives before its `ReserveQuota` is remembered, so the late reserve
  reserves nothing.

## External services

WorkspaceApi calls no external services, such as a payment provider.
