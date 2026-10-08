using Bizcord.WorkspaceApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace Bizcord.WorkspaceApi.Extensions;

public static class ResultExtensions
{
    public static ActionResult ToActionResult(this Result result) =>
        result.IsSuccess ? new NoContentResult() : result.Error.ToProblem();

    public static ActionResult ToProblem(this Error error)
    {
        var status = error switch
        {
            _ when error == WorkspaceErrors.NotFound
                || error == WorkspaceErrors.MemberNotFound
                || error == WorkspaceErrors.ChannelNotFound => StatusCodes.Status404NotFound,
            _ when error == WorkspaceErrors.NotAuthorized
                || error == WorkspaceErrors.CallerNotAMember => StatusCodes.Status403Forbidden,
            _ when error == WorkspaceErrors.AlreadyMember
                || error == WorkspaceErrors.DuplicateChannelName
                || error == WorkspaceErrors.LastAdmin => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };
        return new ObjectResult(new ProblemDetails { Status = status, Title = error.Code, Detail = error.Description })
            { StatusCode = status };
    }
}
