using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bizcord.WorkspaceApi.Authorization;

namespace Bizcord.WorkspaceApi.Tests.ApiTests;

// The user policy only lets a user token through when its sub is a non-empty Guid
public class UserIdClaimTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // Every endpoint that reads the acting user from the token's sub
    public static TheoryData<string, string> Endpoints => new()
    {
        { "POST", "api/v1/workspaces" },
        { "GET", "api/v1/workspaces" },
        { "PUT", $"api/v1/workspaces/{Guid.NewGuid()}" },
        { "DELETE", $"api/v1/workspaces/{Guid.NewGuid()}" },
        { "POST", $"api/v1/workspaces/{Guid.NewGuid()}/channels" },
        { "GET", $"api/v1/workspaces/{Guid.NewGuid()}" },
        { "GET", $"api/v1/workspaces/{Guid.NewGuid()}/channels" },
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task SubNotAGuid_Returns403(string method, string url)
    {
        // Arrange
        var request = CreateRequest(method, url, sub: "bob");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task SubEmptyGuid_Returns403(string method, string url)
    {
        // Arrange
        var request = CreateRequest(method, url, Guid.Empty.ToString());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task ValidSub_IsNotRejected(string method, string url)
    {
        // Arrange
        var request = CreateRequest(method, url, Guid.NewGuid().ToString());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubEmptyGuid_OnCreate_DoesNotStoreWorkspace()
    {
        // Arrange
        var request = CreateRequest("POST", "api/v1/workspaces", Guid.Empty.ToString());

        // Act
        await _client.SendAsync(request);

        // Assert
        Assert.DoesNotContain(factory.Repository.workspaces.Values,
            w => w.Members.Any(m => m.UserId == Guid.Empty));
    }

    private static HttpRequestMessage CreateRequest(string method, string url, string sub)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { name = "test" });
        }

        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", TestApiFactory.CreateToken(Policies.User, sub));
        return request;
    }
}
