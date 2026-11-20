# Events

Services publish and consume messages over RabbitMQ through [Bizcord.MessageClient](../src/Bizcord.MessageClient/README.md).
The records are in [Bizcord.Contracts/Events](../src/Bizcord.Contracts/Events) and
[Bizcord.Contracts/Commands](../src/Bizcord.Contracts/Commands).

- An **event** says that something happened. It ends in `Event`, and anyone can consume it.
- A **command** asks one service to do something. It is named in the imperative, and only that service consumes it.

## Events

| Event                       | Publisher    | Consumers                                                               | Payload                                                 |
|-----------------------------|--------------|-------------------------------------------------------------------------|---------------------------------------------------------|
| MessagePostedEvent          | Message      | `WorkspaceApi`, `Realtime Gateway (planned)`, `Notifications (planned)` | `MessageId` `ChannelId` `AuthorId` `Content` `PostedAt` |
| ChannelActivityUpdatedEvent | WorkspaceApi | `Realtime Gateway (planned)`                                            | `MessageId` `ChannelId` `LastActivityAt` `EventId`      |
| QuotaReservedEvent          | WorkspaceApi | `FileApi`                                                               | `SagaId` `WorkspaceId` `SizeBytes`                      |
| QuotaExceededEvent          | WorkspaceApi | `FileApi`                                                               | `SagaId` `WorkspaceId` `Reason`                         |
| PostMessageSucceededEvent   | Message      | `FileApi`                                                               | `SagaId` `MessageId`                                    |
| PostMessageFailedEvent      | Message      | `FileApi`                                                               | `SagaId` `Reason`                                       |

`WorkspaceApi` only publishes `ChannelActivityUpdatedEvent` when the channel exists and `PostedAt` is newer than the
channel's stored last activity, so unknown channels and late or duplicate messages publish nothing.

`ChannelActivityUpdatedEvent` goes through an outbox (#66). The new last activity and the event are saved in one
transaction, and a background worker publishes the event afterwards and marks it as sent once RabbitMQ has confirmed
it. So an event isn't lost when RabbitMQ is down; it goes out when RabbitMQ is back. The price is at-least-once
delivery: a consumer can get the same event twice and should skip one it has seen by its `EventId`.

## Commands

| Command      | Sent by | Handled by   | Payload                                    |
|--------------|---------|--------------|--------------------------------------------|
| ReserveQuota | FileApi | WorkspaceApi | `SagaId` `WorkspaceId` `SizeBytes`         |
| ReleaseQuota | FileApi | WorkspaceApi | `SagaId`                                   |
| PostMessage  | FileApi | Message      | `SagaId` `ChannelId` `AuthorId` `Content`  |

The commands and the four saga events make up the [upload saga](upload-saga.md).
