using Bizcord.Contracts.Workspaces;
using Bizcord.WorkspaceApi.Models;

namespace Bizcord.WorkspaceApi.Extensions;

public static class RoleExtensions
{
    private static readonly HashSet<Permission> UserPermissions = [Permission.AddMember];

    private static readonly HashSet<Permission> AdminPermissions = new(UserPermissions)
    {
        Permission.AddAdmin,
        Permission.RemoveAdmin,
        Permission.RemoveMember,
        Permission.DeleteChannel,
        Permission.CreateChannel,
        Permission.UpdateWorkspace,
        Permission.DeleteWorkspace
    };

    private static readonly Dictionary<MemberRole, HashSet<Permission>> RolePermissions = new()
    {
        { MemberRole.User, UserPermissions },
        { MemberRole.Admin, AdminPermissions }
    };

    public static bool HasPermission(this MemberRole role, Permission permission)
    {
        if (RolePermissions.TryGetValue(role, out var permissions))
        {
            return permissions.Contains(permission);
        }
        return false;
    }
    
}