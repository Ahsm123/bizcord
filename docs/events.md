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
| ChannelActivityUpdatedEvent | WorkspaceApi | `Realtime Gateway (planned)`                                            | `MessageId` `ChannelId` `LastActivityAt`                |
| QuotaReservedEvent          | WorkspaceApi | `FileApi`                                                               | `SagaId` `WorkspaceId` `SizeBytes`                      |
| QuotaExceededEvent          | WorkspaceApi | `FileApi`                                                               | `SagaId` `WorkspaceId` `Reason`                         |
| PostMessageSucceededEvent   | Message      | `FileApi`                                                               | `SagaId` `MessageId`                                    |
| PostMessageFailedEvent      | Message      | `FileApi`                                                               | `SagaId` `Reason`                                       |

`WorkspaceApi` only publishes `ChannelActivityUpdatedEvent` when the channel exists and `PostedAt` is newer than the
channel's stored last activity, so unknown channels and late or duplicate messages publish nothing.

## Commands

| Command      | Sent by | Handled by   | Payload                                    |
|--------------|---------|--------------|--------------------------------------------|
| ReserveQuota | FileApi | WorkspaceApi | `SagaId` `WorkspaceId` `SizeBytes`         |
| ReleaseQuota | FileApi | WorkspaceApi | `SagaId`                                   |
| PostMessage  | FileApi | Message      | `SagaId` `ChannelId` `AuthorId` `Content`  |

The commands and the four saga events make up the [upload saga](upload-saga.md).
