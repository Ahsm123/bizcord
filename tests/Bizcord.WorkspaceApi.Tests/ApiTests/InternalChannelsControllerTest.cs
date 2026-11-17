using System.Net;
using System.Net.Http.Headers;
using Bizcord.Contracts.Workspaces;
using Bizcord.WorkspaceApi.Authorization;
using Bizcord.WorkspaceApi.Models;

namespace Bizcord.WorkspaceApi.Tests.ApiTests;

public class InternalChannelsControllerTest(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client = factory.CreateClientWithRole(Policies.InternalService);
    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _nonMember = Guid.NewGuid();
    private readonly Guid _channelId = Guid.NewGuid();

    [Fact]
    public async Task IsCallerMemberAsync_NotMember_ReturnsNotFound()
    {
        //Arrange
        await SaveWorkspaceAsync();

        //Act
        var response = await GetMemberStatusAsync(_client, _nonMember, _channelId);

        //Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task IsCallerMemberAsync_IsMember_ReturnsOK()
    {
        //Arrange
        await SaveWorkspaceAsync();

        //Act
        var response = await GetMemberStatusAsync(_client, _user, _channelId);

        //Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task IsCallerMemberAsync_NoToken_Returns401()
    {
        //Arrange
        await SaveWorkspaceAsync();
        var client = factory.CreateClient();

        //Act
        var response = await GetMemberStatusAsync(client, _user, _channelId);

        //Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IsCallerMemberAsync_TokenForAnotherAudience_Returns401()
    {
        //Arrange
        await SaveWorkspaceAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestApiFactory.CreateToken(Policies.InternalService, audience: "message-api"));

        //Act
        var response = await GetMemberStatusAsync(client, _user, _channelId);

        //Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IsCallerMemberAsync_UserToken_Returns403()
    {
        //Arrange
        await SaveWorkspaceAsync();
        var client = factory.CreateClientWithRole(Policies.User);

        //Act
        var response = await GetMemberStatusAsync(client, _user, _channelId);

        //Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PublicEndpoint_InternalServiceToken_Returns403()
    {
        //Act
        var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/workspaces");
        var response = await _client.SendAsync(request);

        //Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task SaveWorkspaceAsync()
    {
        var workspace = Workspace.Create(_admin, "test");
        workspace.AddMember(_admin, _user, MemberRole.User);
        workspace.CreateChannel(_admin, _channelId, "testChannel");
        await factory.Repository.SaveAsync(workspace, CancellationToken.None);
    }

    private static Task<HttpResponseMessage> GetMemberStatusAsync(HttpClient client, Guid userId, Guid channelId) =>
        client.GetAsync($"internal/channels/{channelId}/members/{userId}");
}
