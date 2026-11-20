using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Bizcord.WorkspaceApi.GraphQL;

// GraphQL has one endpoint for both reads and writes, so the gateway can't check the scope per operation the way it
// does per REST route. Each GraphQL field checks the scope here instead, with the same scope names as the gateway.
public static class Scopes
{
    public const string WorkspaceRead = "workspace.read";
    public const string WorkspaceWrite = "workspace.write";
    public const string ChannelWrite = "channel.write";

    public static AuthorizationBuilder AddScopePolicies(this AuthorizationBuilder builder)
    {
        foreach (var scope in new[] { WorkspaceRead, WorkspaceWrite, ChannelWrite })
        {
            builder.AddPolicy(scope, p => p.RequireAssertion(ctx => ctx.User.HasScope(scope)));
        }

        return builder;
    }

    // The scope claim can be one claim per scope or one claim with the scopes separated by spaces
    public static bool HasScope(this ClaimsPrincipal user, string scope) =>
        user.FindAll("scope").SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope);
}
