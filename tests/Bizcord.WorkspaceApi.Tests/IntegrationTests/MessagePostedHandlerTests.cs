using Bizcord.MessageClient.Clients;
using Bizcord.Shared.Events;
using Bizcord.WorkspaceApi.Data;
using Bizcord.WorkspaceApi.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Bizcord.WorkspaceApi.Tests.IntegrationTests;

public class MessagePostedHandlerTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task MessagePostedEvent_IsConsumed_AndResultEventPublished()
    {
        var app = factory.WithWebHostBuilder(b =>
            b.UseSetting("ConnectionStrings:Messaging", "host=localhost;username=kalo;password=kalo"));
        var client = app.Services.GetRequiredService<IMessageClient>();
        var messageId = Guid.NewGuid();

        // The handler only publishes for channels that exist, so create one first
        var owner = Guid.NewGuid();
        var channelId = Guid.NewGuid();
        var workspace = Workspace.Create(owner, $"test-{Guid.NewGuid()}");
        workspace.CreateChannel(owner, channelId, "general");
        using (var scope = app.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>().SaveAsync(workspace, CancellationToken.None);
        }

        var capture = new MessageCapture<ChannelActivityUpdatedEvent>(client);

        await client.PublishAsync(new MessagePostedEvent
        {
            MessageId = messageId,
            ChannelId = channelId,
            AuthorId = Guid.NewGuid(),
            Content = "Hello world",
            // Ahead of the channel's initial now(), even if the container clock drifts a bit
            PostedAt = DateTime.UtcNow.AddMinutes(1)
        });

        var result = await capture.WaitForMessageAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(messageId, result.MessageId);
    }
}