using Bizcord.MessageClient.Clients;
using Bizcord.Shared.Events;
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
        var capture = new MessageCapture<ChannelActivityUpdatedEvent>(client);

        await client.PublishAsync(new MessagePostedEvent
        {
            MessageId = messageId,
            ChannelId = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = "Hello world",
            PostedAt = DateTime.UtcNow
        });

        var result = await capture.WaitForMessageAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(messageId, result.MessageId);
    }
}