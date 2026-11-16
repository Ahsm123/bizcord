using System.Diagnostics;
using System.Net;

namespace Bizcord.WorkspaceApi.Tests.ApiTests;

// The request timeout middleware is disabled with a debugger so run this without one.
public class RequestTimeoutTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    // Without the middleware the request would hang forever, so the client gives up and the test fails instead
    private readonly HttpClient _client = CreateClient(factory);

    private static HttpClient CreateClient(TestApiFactory factory)
    {
        var client = factory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(5);
        return client;
    }

    [Fact]
    public async Task GetChannels_DatabaseHangs_Returns504WithinTwoSeconds()
    {
        // Arrange
        factory.Repository.hang = true;
        // Without a user id the API answers 400 before it reaches the database
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/workspaces/{Guid.NewGuid()}/channels");
        request.Headers.Add("X-User-Id", Guid.NewGuid().ToString());
        var stopwatch = Stopwatch.StartNew();

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(2.5));
    }
}
