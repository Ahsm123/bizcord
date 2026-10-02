using Bizcord.MessageClient.Clients;

namespace Bizcord.WorkspaceApi.Tests.IntegrationTests;

public class MessageCapture<T>
{
    private readonly TaskCompletionSource<T> _received = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public MessageCapture(IMessageClient client)
    {
        client.SubscribeAsync<T>($"capture-{Guid.NewGuid()}", message =>
        {
            _received.TrySetResult(message);
            return Task.CompletedTask;
        }).GetAwaiter().GetResult();
    }

    public Task<T> WaitForMessageAsync(TimeSpan timeout) => _received.Task.WaitAsync(timeout);
}