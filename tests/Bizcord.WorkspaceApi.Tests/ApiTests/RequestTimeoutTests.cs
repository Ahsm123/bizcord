using Bizcord.WorkspaceApi.Authorization;
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
        var client = factory.CreateClientWithRole(Policies.User);
        client.Timeout = TimeSpan.FromSeconds(10);
        return client;
    }

    [Fact]
    public async Task GetChannels_DatabaseHangs_Returns504WithinTwoSeconds()
    {
        // Arrange
        // The first request pays for JIT and token validation, which run before the timeout starts,
        // so it is sent before the stopwatch starts (#98)
        var warmUp = await _client.GetAsync($"api/v1/workspaces/{Guid.NewGuid()}/channels");
        Assert.Equal(HttpStatusCode.NotFound, warmUp.StatusCode);

        factory.Repository.hang = true;
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/workspaces/{Guid.NewGuid()}/channels");
        var stopwatch = Stopwatch.StartNew();

        // Act
        var response = await _client.SendAsync(request);
        stopwatch.Stop();

        // Assert
        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        // Not before the 2 s timeout, and well before the client gives up at 10 s.
        // The room above 2 s is for a busy thread pool when all test projects run at once (#98)
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(4));
    }
}
