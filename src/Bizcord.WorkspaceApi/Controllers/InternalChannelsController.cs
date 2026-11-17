using Bizcord.WorkspaceApi.Authorization;
using Bizcord.WorkspaceApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bizcord.WorkspaceApi.Controllers;

[Authorize(Policy = Policies.InternalService)]
[ApiController]
[Route("internal/channels")]
public class InternalChannelsController(IWorkspaceService workspaceService) : ControllerBase
{
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [HttpGet("{channelId:guid}/members/{userId:guid}")]
    public async Task<ActionResult> GetMemberStatusAsync(Guid userId, Guid channelId, CancellationToken ct)
    {
        var memberResult = await workspaceService.IsChannelMemberAsync(userId, channelId, ct);
        if (memberResult.IsFailure)
        {
            return NotFound();
        }

        return Ok();
    }
}