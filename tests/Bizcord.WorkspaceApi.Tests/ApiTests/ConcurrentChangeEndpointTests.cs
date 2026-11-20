using System.Net;
using System.Net.Http.Json;
using Bizcord.WorkspaceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace Bizcord.WorkspaceApi.Tests.ApiTests;

// #68: when another request changed the workspace first, the request is rejected with 409 instead of overwriting
public class ConcurrentChangeEndpointTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly Guid _admin = Guid.NewGuid();

    [Fact]
    public async Task Rename_WhenChangedByAnotherRequest_Returns409()
    {
        // Arrange
        var workspace = await SaveWorkspaceAsync();
        factory.Repository.conflictOnNextSave = true;

        // Act
        var response = await SendAsync(HttpMethod.Put, $"api/v1/workspaces/{workspace.Id}", new { name = "renamed" });

        // Assert
        await AssertConcurrentChangeAsync(response);
    }

    [Fact]
    public async Task CreateChannel_WhenChangedByAnotherRequest_Returns409()
    {
        // Arrange
        var workspace = await SaveWorkspaceAsync();
        factory.Repository.conflictOnNextSave = true;

        // Act
        var response = await SendAsync(HttpMethod.Post, $"api/v1/workspaces/{workspace.Id}/channels",
            new { name = "general" });

        // Assert
        await AssertConcurrentChangeAsync(response);
    }

    private async Task<Workspace> SaveWorkspaceAsync()
    {
        var workspace = Workspace.Create(_admin, $"ws-{Guid.NewGuid()}");
        await factory.Repository.SaveAsync(workspace, CancellationToken.None);
        return workspace;
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object body) =>
        _client.SendAsync(new HttpRequestMessage(method, url)
        {
            Headers = { Authorization = TestApiFactory.UserAuthHeader(_admin) },
            Content = JsonContent.Create(body)
        });

    private static async Task AssertConcurrentChangeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(WorkspaceErrors.ConcurrentChange.Code, problem!.Title);
    }
}
