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
    public async Task<ActionResult<ChannelDto>> Create([FromUserIdHeader] Guid userId, Guid workspaceId,
        CreateChannelRequest request, CancellationToken ct)
    {
        var result = await workspaceService.CreateChannelAsync(userId, workspaceId, request.Name, ct);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        var dto = result.Value.ToDto(workspaceId);
        return CreatedAtAction(nameof(GetAll), new { workspaceId }, dto);
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ChannelDto>>> GetAll(Guid workspaceId, CancellationToken ct)
    {
        var workspace = await workspaceService.GetByIdAsync(workspaceId, ct);
        if (workspace is null)
        {
            return NotFound();
        }

        var dtos = workspace.Channels.Select(c => c.ToDto(workspaceId)).ToList();
        return Ok(dtos);
    }
}
