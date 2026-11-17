using System.Security.Claims;

namespace Bizcord.WorkspaceApi.Extensions;

public static class ClaimsPrincipalExtensions
{
    // The user policy only lets a request through when HasUserId is true,
    // so every action that runs under it can parse sub without checking
    public static Guid GetUserId(this ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);

    public static bool HasUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id) && id != Guid.Empty;
}
