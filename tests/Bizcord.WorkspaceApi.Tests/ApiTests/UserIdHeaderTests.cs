using System.Net;
using System.Net.Http.Json;

namespace Bizcord.WorkspaceApi.Tests.ApiTests;

public class UserIdHeaderTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // Every endpoint that reads the acting user from X-User-Id
    public static TheoryData<string, string> Endpoints => new()
    {
        { "POST", "api/v1/workspaces" },
        { "GET", "api/v1/workspaces" },
        { "PUT", $"api/v1/workspaces/{Guid.NewGuid()}" },
        { "DELETE", $"api/v1/workspaces/{Guid.NewGuid()}" },
        { "POST", $"api/v1/workspaces/{Guid.NewGuid()}/channels" },
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task MissingHeader_Returns400(string method, string url)
    {
        // Arrange
        var request = CreateRequest(method, url, userId: null);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task EmptyGuidHeader_Returns400(string method, string url)
    {
        // Arrange
        var request = CreateRequest(method, url, Guid.Empty.ToString());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task ValidHeader_IsNotRejected(string method, string url)
    {
        // Arrange
        var request = CreateRequest(method, url, Guid.NewGuid().ToString());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingHeader_OnCreate_DoesNotStoreWorkspace()
    {
        // Arrange
        var request = CreateRequest("POST", "api/v1/workspaces", userId: null);

        // Act
        await _client.SendAsync(request);

        // Assert
        Assert.DoesNotContain(factory.Repository.workspaces.Values,
            w => w.Members.Any(m => m.UserId == Guid.Empty));
    }

    private static HttpRequestMessage CreateRequest(string method, string url, string? userId)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { name = "test" });
        }

        if (userId is not null)
        {
            request.Headers.Add("X-User-Id", userId);
        }

        return request;
    }
}
