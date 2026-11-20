using Bizcord.WorkspaceApi.Services;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Bizcord.WorkspaceApi.Tests.IntegrationTests;

// #67 against the real Postgres, since only the unique key in the database can stop two requests
// that arrive at the same time
public class IdempotencyKeyRepositoryTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _app = factory.WithWebHostBuilder(b => b
        .UseSetting("ConnectionStrings:Messaging", "host=localhost;username=kalo;password=kalo")
        .UseSetting("Jwt:IssuerSigningKey", "test-signing-key-that-is-at-least-32-bytes-long"));

    [Fact]
    public async Task SameKeyAgain_ReturnsTheOriginalWorkspace()
    {
        // Arrange
        var user = Guid.NewGuid();
        var name = $"idempotent-{Guid.NewGuid()}";

        // Act
        var first = await CreateAsync(user, name, "key-1");
        var retry = await CreateAsync(user, name, "key-1");

        // Assert
        Assert.Equal(first, retry);
        Assert.Equal(1, await CountWorkspacesNamedAsync(name));
    }

    [Fact]
    public async Task SameKeyFiveTimesAtTheSameTime_CreatesOneWorkspace()
    {
        // Arrange
        var user = Guid.NewGuid();
        var name = $"idempotent-{Guid.NewGuid()}";

        // Act: each call has its own scope, like separate HTTP requests
        var ids = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => CreateAsync(user, name, "same-key")));

        // Assert
        Assert.Single(ids.Distinct());
        Assert.Equal(1, await CountWorkspacesNamedAsync(name));
    }

    [Fact]
    public async Task DifferentKeys_CreateDifferentWorkspaces()
    {
        // Arrange
        var user = Guid.NewGuid();
        var name = $"idempotent-{Guid.NewGuid()}";

        // Act
        await CreateAsync(user, name, "key-a");
        await CreateAsync(user, name, "key-b");

        // Assert
        Assert.Equal(2, await CountWorkspacesNamedAsync(name));
    }

    private async Task<Guid> CreateAsync(Guid user, string name, string key)
    {
        using var scope = _app.Services.CreateScope();
        var workspace = await scope.ServiceProvider.GetRequiredService<IWorkspaceService>()
            .CreateAsync(user, name, CancellationToken.None, key);
        return workspace.Id;
    }

    private async Task<int> CountWorkspacesNamedAsync(string name)
    {
        await using var connection = await _app.Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>("select count(*) from workspaces where name = @name",
            new { name });
    }
}
