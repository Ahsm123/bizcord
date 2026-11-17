using Bizcord.WorkspaceApi.Extensions;
using Bizcord.WorkspaceApi.Requests;
using Bizcord.WorkspaceApi.Services;
using Bizcord.Contracts.Workspaces;
using Microsoft.AspNetCore.Mvc;

namespace Bizcord.WorkspaceApi.Controllers;

[ApiController]
[Route("api/v1/workspaces/{workspaceId:guid}/channels")]
public class ChannelsController(IWorkspaceService workspaceService)
    : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ChannelDto>> Create(Guid workspaceId, CreateChannelRequest request,
        CancellationToken ct)
    {
        var result = await workspaceService.CreateChannelAsync(User.GetUserId(), workspaceId, request.Name, ct);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        var dto = result.Value.ToDto(workspaceId);
        return CreatedAtAction(nameof(GetAll), new { workspaceId }, dto);
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ChannelDto>>>
        GetAll(Guid workspaceId, CancellationToken ct)
    {
        var result = await workspaceService.GetByIdAsync(workspaceId, User.GetUserId(), ct);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        var dtos = result.Value.Channels.Select(c =>
            c.ToDto(workspaceId)).ToList();
        return Ok(dtos);
    }
}