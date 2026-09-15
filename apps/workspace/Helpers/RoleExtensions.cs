using Bizcord.WorkspaceApi.Models;

namespace Bizcord.WorkspaceApi.Helpers;

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
    };

    private static readonly Dictionary<Role, HashSet<Permission>> RolePermissions = new()
    {
        { Role.User, UserPermissions },
        { Role.Admin, AdminPermissions }
    };

    public static bool HasPermission(this Role role, Permission permission)
    {
        if (RolePermissions.TryGetValue(role, out var permissions))
        {
            return permissions.Contains(permission);
        }
        return false;
    }
    
}