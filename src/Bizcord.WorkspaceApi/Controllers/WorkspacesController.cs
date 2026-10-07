using Bizcord.WorkspaceApi.Requests;
using Bizcord.WorkspaceApi.Services;
using Bizcord.Contracts.Workspaces;
using Microsoft.AspNetCore.Mvc;

namespace Bizcord.WorkspaceApi.Controllers;

[ApiController]
[Route("api/v1/workspaces")]
public class WorkspacesController(IWorkspaceService workspaceService)
    : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkspaceDto>> Create([FromHeader(Name = "X-User-Id")] Guid ownerId,
        CreateWorkspaceRequest request, CancellationToken ct)
    {
        var workspace = await workspaceService.CreateAsync(ownerId, request.Name, ct);
        var dto = new WorkspaceDto(workspace.Id, workspace.Name);
        return CreatedAtAction(nameof(GetById), new { id = workspace.Id }, dto);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkspaceDto>> GetById(Guid id, CancellationToken ct)
    {
        var workspace = await workspaceService.GetByIdAsync(id, ct);
        if (workspace is null)
        {
            return NotFound();
        }

        var dto = new WorkspaceDto(workspace.Id, workspace.Name);
        return Ok(dto);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkspaceDto>>> GetAll([FromHeader(Name = "X-User-Id")] Guid userId,
        CancellationToken ct)
    {
        var userWorkspaces = await workspaceService.GetAllAsync(userId, ct);
        return Ok(userWorkspaces);
    }
}