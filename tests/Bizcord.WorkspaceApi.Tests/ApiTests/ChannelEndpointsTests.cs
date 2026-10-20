using System.Net;
using System.Net.Http.Json;
using Bizcord.Contracts.Workspaces;
using Bizcord.WorkspaceApi.Models;

namespace Bizcord.WorkspaceApi.Tests.ApiTests;

public class ChannelEndpointsTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();

    [Fact]
    public async Task CreateChannel_Admin_Returns201WithChannel()
    {
        // Arrange
        var workspace = await SaveWorkspaceAsync();

        // Act
        var response = await PostChannelAsync(workspace.Id, _admin, "general");

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var channel = await response.Content.ReadFromJsonAsync<ChannelDto>();
        Assert.NotNull(channel);
        Assert.NotEqual(Guid.Empty, channel.Id);
        Assert.Equal("general", channel.Name);
        Assert.Equal(workspace.Id, channel.WorkspaceId);
    }

    [Fact]
    public async Task CreateChannel_ThenGetChannels_ReturnsIt()
    {
        // Arrange
        var workspace = await SaveWorkspaceAsync();
        var created = await (await PostChannelAsync(workspace.Id, _admin, "general"))
            .Content.ReadFromJsonAsync<ChannelDto>();

        // Act
        var channels = await _client.GetFromJsonAsync<List<ChannelDto>>(
            $"api/v1/workspaces/{workspace.Id}/channels");

        // Assert
        Assert.Equal([created], channels);
    }

    [Fact]
    public async Task CreateChannel_User_Returns403()
    {
        // Arrange
        var workspace = await SaveWorkspaceAsync();

        // Act
        var response = await PostChannelAsync(workspace.Id, _user, "general");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateChannel_NonMember_Returns403()
    {
        // Arrange
        var workspace = await SaveWorkspaceAsync();

        // Act
        var response = await PostChannelAsync(workspace.Id, Guid.NewGuid(), "general");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateChannel_UnknownWorkspace_Returns404()
    {
        // Act
        var response = await PostChannelAsync(Guid.NewGuid(), _admin, "general");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateChannel_TakenName_Returns409()
    {
        // Arrange
        var workspace = await SaveWorkspaceAsync();
        await PostChannelAsync(workspace.Id, _admin, "general");

        // Act
        var response = await PostChannelAsync(workspace.Id, _admin, "general");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("this name is far too long for a channel because channel names may only be one hundred characters long")]
    public async Task CreateChannel_InvalidName_Returns400(string name)
    {
        // Arrange
        var workspace = await SaveWorkspaceAsync();

        // Act
        var response = await PostChannelAsync(workspace.Id, _admin, name);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetChannels_ReturnsChannelsOfWorkspace()
    {
        // Arrange
        var workspace = Workspace.Create(_admin, "test");
        var general = Guid.NewGuid();
        var random = Guid.NewGuid();
        workspace.CreateChannel(_admin, general, "general");
        workspace.CreateChannel(_admin, random, "random");
        await factory.Repository.SaveAsync(workspace, CancellationToken.None);

        // Act
        var channels = await _client.GetFromJsonAsync<List<ChannelDto>>(
            $"api/v1/workspaces/{workspace.Id}/channels");

        // Assert
        Assert.NotNull(channels);
        Assert.Equal(2, channels.Count);
        Assert.Contains(new ChannelDto(general, "general", workspace.Id), channels);
        Assert.Contains(new ChannelDto(random, "random", workspace.Id), channels);
    }

    [Fact]
    public async Task GetChannels_UnknownWorkspace_Returns404()
    {
        // Act
        var response = await _client.GetAsync($"api/v1/workspaces/{Guid.NewGuid()}/channels");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<Workspace> SaveWorkspaceAsync()
    {
        var workspace = Workspace.Create(_admin, "test");
        workspace.AddMember(_admin, _user, MemberRole.User);
        await factory.Repository.SaveAsync(workspace, CancellationToken.None);
        return workspace;
    }

    private Task<HttpResponseMessage> PostChannelAsync(Guid workspaceId, Guid userId, string name)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/workspaces/{workspaceId}/channels")
        {
            Content = JsonContent.Create(new { name })
        };
        request.Headers.Add("X-User-Id", userId.ToString());
        return _client.SendAsync(request);
    }
}
