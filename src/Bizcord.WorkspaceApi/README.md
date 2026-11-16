# Failure points

| Dependency                        | Direction | Type  |
|-----------------------------------|-----------|-------|
| Postgres                          | outgoing  | sync  |
| RabbitMQ                          | outgoing  | async |
| Gateway (Ocelot)                  | incoming  | sync  |
| MessageApi via MessagePostedEvent | incoming  | async |

## Postgres

- WorkspaceApi's sync calls to Postgres have no timeout set, so a slow database can exhaust the connection pool and fail every request.
- If Postgres is down, every request fails with a 500 and nothing retries.
- If Postgres is down, the MessagePostedEvent handler fails and the message is placed in the error queue, so channel activity is never updated.
- Two requests that change the same workspace at the same time both succeed, but the last save overwrites the other, so its new members or channels are lost.

## RabbitMQ

- WorkspaceApi updates Postgres before publishing ChannelActivityUpdatedEvent, so if RabbitMQ is slow or down at that moment, the event is never sent, and a retry can't fix it.
- Unknown: if RabbitMQ is down when WorkspaceApi starts, the message worker may stop the whole service, including HTTP.

## Gateway (Ocelot)

- If the gateway retries a POST /workspaces, a second workspace is created, because the request has no idempotency key.
- WorkspaceApi does not throttle incoming requests, so a spike or retries can overload it and that can cascade to Postgres.

## MessageApi (MessagePostedEvent)

- A burst of MessagePostedEvents only grows the queue, because the handler consumes at its own pace, so channel activity is delayed but not lost.

## External services

- WorkspaceApi calls no external services, such as a payment provider.

Covered: SaveAsync writes all tables in one transaction, and a double MessagePostedEvent has no double effect.
