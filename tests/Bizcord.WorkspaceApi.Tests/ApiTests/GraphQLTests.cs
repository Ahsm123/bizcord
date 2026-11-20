using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bizcord.WorkspaceApi.GraphQL;
using Bizcord.WorkspaceApi.Models;

namespace Bizcord.WorkspaceApi.Tests.ApiTests;

public class GraphQLTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private const string AllScopes = "workspace.read workspace.write channel.write";

    private readonly HttpClient _client = factory.CreateClient();
    private readonly Guid _admin = Guid.NewGuid();
    private readonly Guid _member = Guid.NewGuid();

    [Fact]
    public async Task MyWorkspaces_ReturnsOnlyTheCallersWorkspaces()
    {
        // Arrange
        var mine = await SaveWorkspaceAsync();
        await SaveWorkspaceAsync(owner: Guid.NewGuid());

        // Act
        var result = await QueryAsync(_admin, AllScopes, "{ myWorkspaces { id name } }");

        // Assert
        var workspaces = result.GetProperty("data").GetProperty("myWorkspaces").EnumerateArray().ToList();
        var only = Assert.Single(workspaces);
        Assert.Equal(mine.Id, only.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Workspace_ReturnsChannelsAndMembersInOneQuery()
    {
        // Arrange
        var workspace = await SaveWorkspaceAsync();

        // Act
        var result = await QueryAsync(_member, AllScopes,
            "query($id: UUID!) { workspace(id: $id) { name channels { name } members { userId role } } }",
            new { id = workspace.Id });

        // Assert
        var data = result.GetProperty("data").GetProperty("workspace");
        Assert.Equal("general", data.GetProperty("channels")[0].GetProperty("name").GetString());
        var roles = data.GetProperty("members").EnumerateArray()
            .ToDictionary(m => m.GetProperty("userId").GetGuid(), m => m.GetProperty("role").GetString());
        Assert.Equal("ADMIN", roles[_admin]);
        Assert.Equal("USER", roles[_member]);
    }

    [Fact]
    public async Task Workspace_Unknown_ReturnsNotFoundError()
    {
        // Act
        var result = await QueryAsync(_admin, AllScopes, "query($id: UUID!) { workspace(id: $id) { name } }",
            new { id = Guid.NewGuid() });

        // Assert
        AssertError(result, WorkspaceErrors.NotFound.Code, 404);
    }

    [Fact]
    public async Task Workspace_NotAMember_ReturnsForbiddenError()
    {
        // Arrange
        var workspace = await SaveWorkspaceAsync();

        // Act
        var result = await QueryAsync(Guid.NewGuid(), AllScopes, "query($id: UUID!) { workspace(id: $id) { name } }",
            new { id = workspace.Id });

        // Assert
        AssertError(result, WorkspaceErrors.CallerNotAMember.Code, 403);
    }

    [Fact]
    public async Task CreateWorkspace_MakesTheCallerAdmin()
    {
        // Act
        var result = await QueryAsync(_admin, AllScopes, "mutation { createWorkspace(name: \"team\") { id name } }");

        // Assert
        var id = result.GetProperty("data").GetProperty("createWorkspace").GetProperty("id").GetGuid();
        var stored = factory.Repository.workspaces[id];
        Assert.Equal("team", stored.Name);
        Assert.Contains(stored.Members, m => m.UserId == _admin);
    }

    [Fact]
    public async Task CreateWorkspace_EmptyName_ReturnsValidationError()
    {
        // Act
        var result = await QueryAsync(_admin, AllScopes, "mutation { createWorkspace(name: \" \") { id } }");

        // Assert
        AssertError(result, GraphQLErrors.ValidationCode, 400);
    }

    [Fact]
    public async Task CreateChannel_Admin_CreatesIt()
    {
        // Arrange
        var workspace = await SaveWorkspaceAsync();

        // Act
        var result = await QueryAsync(_admin, AllScopes,
            "mutation($id: UUID!) { createChannel(workspaceId: $id, name: \"random\") { id name } }",
            new { id = workspace.Id });

        // Assert
        Assert.Equal("random",
            result.GetProperty("data").GetProperty("createChannel").GetProperty("name").GetString());
        Assert.Contains(factory.Repository.workspaces[workspace.Id].Channels, c => c.Name == "random");
    }

    [Fact]
    public async Task CreateChannel_NotAdmin_ReturnsForbiddenError()
    {
        // Arrange
        var workspace = await SaveWorkspaceAsync();

        // Act
        var result = await QueryAsync(_member, AllScopes,
            "mutation($id: UUID!) { createChannel(workspaceId: $id, name: \"random\") { id } }",
            new { id = workspace.Id });

        // Assert
        AssertError(result, WorkspaceErrors.NotAuthorized.Code, 403);
    }

    [Fact]
    public async Task CreateChannel_DuplicateName_ReturnsConflictError()
    {
        // Arrange
        var workspace = await SaveWorkspaceAsync();

        // Act
        var result = await QueryAsync(_admin, AllScopes,
            "mutation($id: UUID!) { createChannel(workspaceId: $id, name: \"general\") { id } }",
            new { id = workspace.Id });

        // Assert
        AssertError(result, WorkspaceErrors.DuplicateChannelName.Code, 409);
    }

    [Fact]
    public async Task Mutation_WithOnlyReadScope_IsNotAuthorizedAndChangesNothing()
    {
        // Arrange
        var before = factory.Repository.workspaces.Count;

        // Act
        var result = await QueryAsync(_admin, Scopes.WorkspaceRead, "mutation { createWorkspace(name: \"x\") { id } }");

        // Assert
        Assert.True(result.TryGetProperty("errors", out _));
        Assert.Equal(before, factory.Repository.workspaces.Count);
    }

    [Fact]
    public async Task Query_WithoutReadScope_IsNotAuthorized()
    {
        // Act
        var result = await QueryAsync(_admin, Scopes.WorkspaceWrite, "{ myWorkspaces { id } }");

        // Assert
        var error = result.GetProperty("errors")[0];
        Assert.Equal("AUTH_NOT_AUTHORIZED", error.GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task NoToken_Returns401()
    {
        // Act
        var response = await _client.PostAsJsonAsync("graphql", new { query = "{ myWorkspaces { id } }" });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Workspace> SaveWorkspaceAsync(Guid? owner = null)
    {
        var admin = owner ?? _admin;
        var workspace = Workspace.Create(admin, $"ws-{Guid.NewGuid()}");
        workspace.AddMember(admin, _member, Bizcord.Contracts.Workspaces.MemberRole.User);
        workspace.CreateChannel(admin, Guid.NewGuid(), "general");
        await factory.Repository.SaveAsync(workspace, CancellationToken.None);
        return workspace;
    }

    private async Task<JsonElement> QueryAsync(Guid userId, string scope, string query, object? variables = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "graphql")
        {
            Headers = { Authorization = TestApiFactory.UserAuthHeader(userId, scope) },
            Content = JsonContent.Create(new { query, variables })
        };
        var response = await _client.SendAsync(request);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static void AssertError(JsonElement result, string code, int status)
    {
        var extensions = result.GetProperty("errors")[0].GetProperty("extensions");
        Assert.Equal(code, extensions.GetProperty("code").GetString());
        Assert.Equal(status, extensions.GetProperty("status").GetInt32());
    }
}
