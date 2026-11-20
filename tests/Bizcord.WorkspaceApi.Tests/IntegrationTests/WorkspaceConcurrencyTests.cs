using Bizcord.Contracts.Workspaces;
using Bizcord.WorkspaceApi.Data;
using Bizcord.WorkspaceApi.Models;
using Bizcord.WorkspaceApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Bizcord.WorkspaceApi.Tests.IntegrationTests;

// #68: two requests that load the same workspace and both save must not overwrite each other.
// Runs against the real Postgres, since every load there gives its own copy of the workspace.
public class WorkspaceConcurrencyTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _app = factory.WithWebHostBuilder(b => b
        .UseSetting("ConnectionStrings:Messaging", "host=localhost;username=kalo;password=kalo")
        .UseSetting("Jwt:IssuerSigningKey", "test-signing-key-that-is-at-least-32-bytes-long"));

    private readonly Guid _admin = Guid.NewGuid();

    [Fact]
    public async Task TwoLoadsThatCreateDifferentChannels_TheSecondSaveIsRejected()
    {
        // Arrange
        var id = await CreateWorkspaceAsync();
        var first = await LoadAsync(id);
        var second = await LoadAsync(id);
        first.CreateChannel(_admin, Guid.NewGuid(), "first");
        second.CreateChannel(_admin, Guid.NewGuid(), "second");

        // Act
        var firstSaved = await SaveAsync(first);
        var secondSaved = await SaveAsync(second);

        // Assert
        Assert.True(firstSaved);
        Assert.False(secondSaved);
        Assert.Equal(["first"], (await LoadAsync(id)).Channels.Select(c => c.Name));
    }

    [Fact]
    public async Task TwoLoadsThatAddDifferentMembers_TheSecondSaveIsRejected()
    {
        // Arrange
        var id = await CreateWorkspaceAsync();
        var first = await LoadAsync(id);
        var second = await LoadAsync(id);
        var firstMember = Guid.NewGuid();
        first.AddMember(_admin, firstMember, MemberRole.User);
        second.AddMember(_admin, Guid.NewGuid(), MemberRole.User);

        // Act
        await SaveAsync(first);
        var secondSaved = await SaveAsync(second);

        // Assert
        Assert.False(secondSaved);
        var members = (await LoadAsync(id)).Members.Select(m => m.UserId).ToHashSet();
        Assert.Equal([_admin, firstMember], members);
    }

    [Fact]
    public async Task SaveAfterReload_Succeeds()
    {
        // Arrange
        var id = await CreateWorkspaceAsync();
        var stale = await LoadAsync(id);
        var other = await LoadAsync(id);
        other.Rename(_admin, "renamed");
        await SaveAsync(other);

        // Act
        var fresh = await LoadAsync(id);
        fresh.CreateChannel(_admin, Guid.NewGuid(), "after-reload");
        var saved = await SaveAsync(fresh);

        // Assert
        Assert.True(saved);
        Assert.False(await SaveAsync(stale));
        var stored = await LoadAsync(id);
        Assert.Equal("renamed", stored.Name);
        Assert.Contains(stored.Channels, c => c.Name == "after-reload");
    }

    [Fact]
    public async Task ParallelCreateChannelRequests_NoChannelIsLostAndTheRestGet409()
    {
        // Arrange
        var id = await CreateWorkspaceAsync();
        var names = Enumerable.Range(1, 5).Select(i => $"channel-{i}").ToList();

        // Act: each request has its own scope, like separate HTTP requests
        var results = await Task.WhenAll(names.Select(async name =>
        {
            using var scope = _app.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IWorkspaceService>();
            return (name, result: await service.CreateChannelAsync(_admin, id, name, CancellationToken.None));
        }));

        // Assert: every request either saved its channel or was told to retry, none was silently lost
        var stored = (await LoadAsync(id)).Channels.Select(c => c.Name).ToHashSet();
        foreach (var (name, result) in results)
        {
            if (result.IsSuccess)
            {
                Assert.Contains(name, stored);
            }
            else
            {
                Assert.Equal(WorkspaceErrors.ConcurrentChange, result.Error);
                Assert.DoesNotContain(name, stored);
            }
        }

        Assert.Contains(results, r => r.result.IsSuccess);
    }

    private async Task<Guid> CreateWorkspaceAsync()
    {
        var workspace = Workspace.Create(_admin, $"concurrency-{Guid.NewGuid()}");
        Assert.True(await SaveAsync(workspace));
        return workspace.Id;
    }

    private async Task<Workspace> LoadAsync(Guid id)
    {
        using var scope = _app.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>()
            .GetByIdAsync(id, CancellationToken.None))!;
    }

    private async Task<bool> SaveAsync(Workspace workspace)
    {
        using var scope = _app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>()
            .SaveAsync(workspace, CancellationToken.None);
    }
}
