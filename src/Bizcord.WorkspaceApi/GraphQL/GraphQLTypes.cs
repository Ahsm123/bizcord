using Bizcord.Contracts.Workspaces;
using Bizcord.WorkspaceApi.Models;
using HotChocolate;

namespace Bizcord.WorkspaceApi.GraphQL;

// The GraphQL types are shaped for the client and kept apart from the domain models, so the domain can change
// without changing the schema, and the schema can't expose more of the domain than these types do.

[GraphQLName("WorkspaceSummary")]
public record WorkspaceSummaryType(Guid Id, string Name);

[GraphQLName("Workspace")]
public record WorkspaceType(Guid Id, string Name, IReadOnlyList<ChannelType> Channels, IReadOnlyList<MemberType> Members)
{
    public static WorkspaceType From(Workspace workspace) => new(
        workspace.Id,
        workspace.Name,
        workspace.Channels.Select(ChannelType.From).OrderBy(c => c.Name).ToList(),
        workspace.Members.Select(m => new MemberType(m.UserId, m.Role)).ToList());
}

[GraphQLName("Channel")]
public record ChannelType(Guid Id, string Name)
{
    public static ChannelType From(Channel channel) => new(channel.Id, channel.Name);
}

[GraphQLName("Member")]
public record MemberType(Guid UserId, MemberRole Role);
