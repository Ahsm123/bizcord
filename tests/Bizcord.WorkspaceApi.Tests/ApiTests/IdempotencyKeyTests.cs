using System.Net;
using System.Net.Http.Json;
using Bizcord.Contracts.Workspaces;

namespace Bizcord.WorkspaceApi.Tests.ApiTests;

// #67: a retried POST /workspaces with the same Idempotency-Key gets the original workspace back
public class IdempotencyKeyTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SameKeyTwice_ReturnsTheOriginalWorkspaceAndCreatesNothing()
    {
        // Arrange
        var user = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();

        // Act
        var first = await PostAsync(user, "team", key);
        var retry = await PostAsync(user, "team", key);

        // Assert
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        var created = await first.Content.ReadFromJsonAsync<WorkspaceDto>();
        Assert.Equal(created, await retry.Content.ReadFromJsonAsync<WorkspaceDto>());
        Assert.Single(await factory.Repository.ListForUserAsync(user, CancellationToken.None));
    }

    [Fact]
    public async Task SameKeyFromTwoUsers_CreatesTwoWorkspaces()
    {
        // Arrange
        var key = Guid.NewGuid().ToString();

        // Act
        var first = await (await PostAsync(Guid.NewGuid(), "team", key)).Content.ReadFromJsonAsync<WorkspaceDto>();
        var second = await (await PostAsync(Guid.NewGuid(), "team", key)).Content.ReadFromJsonAsync<WorkspaceDto>();

        // Assert
        Assert.NotEqual(first!.Id, second!.Id);
    }

    [Fact]
    public async Task NoKey_EveryRequestCreatesAWorkspace()
    {
        // Arrange
        var user = Guid.NewGuid();

        // Act
        await PostAsync(user, "team", key: null);
        await PostAsync(user, "team", key: null);

        // Assert
        Assert.Equal(2, (await factory.Repository.ListForUserAsync(user, CancellationToken.None)).Count);
    }

    [Fact]
    public async Task KeyLongerThan100Characters_Returns400AndCreatesNothing()
    {
        // Arrange
        var user = Guid.NewGuid();
        var key = new string('k', 101);

        // Act
        var response = await PostAsync(user, "team", key);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await factory.Repository.ListForUserAsync(user, CancellationToken.None));
    }

    private Task<HttpResponseMessage> PostAsync(Guid userId, string name, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/workspaces")
        {
            Headers = { Authorization = TestApiFactory.UserAuthHeader(userId) },
            Content = JsonContent.Create(new { name })
        };
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        return _client.SendAsync(request);
    }
}
