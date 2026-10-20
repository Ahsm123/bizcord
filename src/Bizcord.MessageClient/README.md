# Bizcord.MessageClient

Wrapper around RabbitMQ with EasyNetQ, so services dont talk to the broker directly.

## IMessageClient

| Method              | What it does                                                                                                |
|---------------------|-------------------------------------------------------------------------------------------------------------|
| `PublishAsync<T>`   | Publishes a message of type `T`                                                                             |
| `SubscribeAsync<T>` | Subscribes a handler to messages of type `T` with a `subscriberId`. Throws if that id is already subscribed |
| `UnsubscribeAsync`  | Stops the subscription with that `subscriberId`                                                             |

## Setup

```csharp
builder.Services.AddMessaging(builder.Configuration, Assembly.GetExecutingAssembly());
```

Uses the connection string `ConnectionStrings:Messaging`

## Handling messages

Implement `IMessageHandler<T>` in the assembly passed to `AddMessaging`. Nothing else needs registering:

- every `IMessageHandler<T>` in the assembly is found with reflection and registered as scoped
- one `MessageHandlerWorker<T>` is started per message type. It subscribes and passes each message to the handlers
  for `T` in a new scope

The subscriber id is `<assembly name>.<message name>`, e.g. `Bizcord.WorkspaceApi.MessagePostedEvent`. EasyNetQ names
the queue after it, so all instances of a service share one queue per message type and each service gets its own.
Renaming the assembly or the message type gives a new queue.

## Swapping broker

Services only depend on `IMessageClient`. To use another broker, just write a new class that implements
`IMessageClient` and register it in `AddMessaging` instead of `MessageClient`.
