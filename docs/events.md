# Events

Services publish and consume events over RabbitMQ through [Bizcord.MessageClient](../src/Bizcord.MessageClient/README.md).
The event records are in [Bizcord.Contracts/Events](../src/Bizcord.Contracts/Events).

| Event                       | Publisher    | Consumers                                                               | Payload                                                 |
|-----------------------------|--------------|-------------------------------------------------------------------------|---------------------------------------------------------|
| MessagePostedEvent          | MessageApi   | `WorkspaceApi`, `Realtime Gateway (planned)`, `Notifications (planned)` | `MessageId` `ChannelId` `AuthorId` `Content` `PostedAt` |
| ChannelActivityUpdatedEvent | WorkspaceApi | `Realtime Gateway (planned)`                                            | `MessageId` `ChannelId` `LastActivityAt`                |

`WorkspaceApi` only publishes `ChannelActivityUpdatedEvent` when the channel exists and `PostedAt` is newer than the
channel's stored last activity, so unknown channels and late or duplicate messages publish nothing.
