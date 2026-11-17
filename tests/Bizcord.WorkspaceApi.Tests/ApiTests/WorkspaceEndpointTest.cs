using System.Net;
using System.Net.Http.Json;
using Bizcord.Contracts.Workspaces;
using Bizcord.WorkspaceApi.Models;

namespace Bizcord.WorkspaceApi.Tests.ApiTests;

public class WorkspaceEndpointTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _nonMember = Guid.NewGuid();

    [Fact]
    public async Task CanRead_Member_Returns200()
    {
        //Arrange
        var workspace = await SaveWorkspaceAsync();

        //Act
        var response = await GetWorkspaceByIdAsync(workspace.Id, _user);

        //Assert
        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task CanRead_NonMember_Returns403()
    {
        //Arrange
        var workspace = await SaveWorkspaceAsync();
        
        //Act
        var response = await  GetWorkspaceByIdAsync(workspace.Id, _nonMember);

        //Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    


    private async Task<Workspace> SaveWorkspaceAsync()
    {
        var workspace = Workspace.Create(_admin, "test");
        workspace.AddMember(_admin, _user, MemberRole.User);
        await factory.Repository.SaveAsync(workspace, CancellationToken.None);
        return workspace;
    }

    private Task<HttpResponseMessage> GetWorkspaceByIdAsync(Guid workspaceId, Guid userId)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/workspaces/{workspaceId}")
        {
        };
        request.Headers.Authorization = TestApiFactory.UserAuthHeader(userId);
        return _client.SendAsync(request);
    }
}