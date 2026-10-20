using Bizcord.MessageClient.Clients;
using Bizcord.MessageClient.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bizcord.MessageClient.Tests.UnitTests;

public record TestMessage(string Text);

public record OtherTestMessage(int Number);

public class HandledMessages
{
    public readonly List<object> messages = new();
}

// Found by AddMessaging because it lives in the scanned test assembly
public class TestMessageHandler(HandledMessages handled) : IMessageHandler<TestMessage>
{
    public Task Handle(TestMessage message, CancellationToken cancellationToken)
    {
        handled.messages.Add(message);
        return Task.CompletedTask;
    }
}

public class OtherTestMessageHandler(HandledMessages handled) : IMessageHandler<OtherTestMessage>
{
    public Task Handle(OtherTestMessage message, CancellationToken cancellationToken)
    {
        handled.messages.Add(message);
        return Task.CompletedTask;
    }
}

public class MessageHandlerRegistrationTests
{
    private const string TestMessageSubscriber = "Bizcord.MessageClient.Tests.TestMessage";
    private const string OtherTestMessageSubscriber = "Bizcord.MessageClient.Tests.OtherTestMessage";

    private readonly FakeMessageClient _client = new();
    private readonly HandledMessages _handled = new();

    [Fact]
    public async Task AddMessaging_SubscribesOncePerMessageType_WithAssemblyAndMessageName()
    {
        // Arrange
        await using var provider = BuildProvider();

        // Act
        await StartWorkersAsync(provider);

        // Assert
        Assert.Equal([OtherTestMessageSubscriber, TestMessageSubscriber], _client.subscriptions.Keys.Order());
    }

    [Fact]
    public async Task AddMessaging_DeliveredMessage_ReachesItsHandler()
    {
        // Arrange
        await using var provider = BuildProvider();
        await StartWorkersAsync(provider);

        // Act
        await _client.DeliverAsync(TestMessageSubscriber, new TestMessage("hello"));
        await _client.DeliverAsync(OtherTestMessageSubscriber, new OtherTestMessage(42));

        // Assert
        Assert.Equal([new TestMessage("hello"), new OtherTestMessage(42)], _handled.messages);
    }

    [Fact]
    public async Task AddMessaging_RegistersHandlersAsScoped()
    {
        // Arrange
        await using var provider = BuildProvider();

        // Act
        await using var first = provider.CreateAsyncScope();
        await using var second = provider.CreateAsyncScope();
        var handler = first.ServiceProvider.GetRequiredService<IMessageHandler<TestMessage>>();

        // Assert
        Assert.IsType<TestMessageHandler>(handler);
        Assert.Same(handler, first.ServiceProvider.GetRequiredService<IMessageHandler<TestMessage>>());
        Assert.NotSame(handler, second.ServiceProvider.GetRequiredService<IMessageHandler<TestMessage>>());
    }

    private ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Messaging"] = "host=localhost"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMessaging(configuration, typeof(TestMessageHandler).Assembly);

        // Replace the RabbitMQ client so nothing connects to a broker
        services.AddSingleton<IMessageClient>(_client);
        services.AddSingleton(_handled);

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    private static async Task StartWorkersAsync(IServiceProvider provider)
    {
        foreach (var worker in provider.GetServices<IHostedService>().OfType<BackgroundService>())
        {
            await worker.StartAsync(CancellationToken.None);
            // ExecuteAsync runs in the background, and the workers' ExecuteAsync is done once it has subscribed
            await worker.ExecuteTask!;
        }
    }
}
