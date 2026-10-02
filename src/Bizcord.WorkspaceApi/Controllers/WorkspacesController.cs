using Bizcord.WorkspaceApi.Requests;
using Bizcord.WorkspaceApi.Services;
using Bizcord.WorkspaceContracts.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Bizcord.WorkspaceApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class WorkspacesController(IWorkspaceService workspaceService)
    : ControllerBase
{

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkspaceDto>> Create([FromHeader(Name = "X-User-Id")] Guid ownerId, CreateWorkspaceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
           return BadRequest("Workspace needs a valid name");
        }
        
        var workspace = await workspaceService.CreateAsync(ownerId, request.Name);
        if (workspace == null)
        {
            return BadRequest("Failed to create workspace");
        }
        
        var dto = new WorkspaceDto(workspace.Id, workspace.Name);
        
        return CreatedAtAction(nameof(GetById), new { id = workspace.Id }, dto);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkspaceDto>> GetById(Guid id)
    {
        var workspace = await workspaceService.GetByIdAsync(id);
        if (workspace == null)
        {
            return NotFound();
        }
        
        var dto = new WorkspaceDto(workspace.Id, workspace.Name);
        return Ok(dto);
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<WorkspaceDto>>> List([FromHeader(Name = "X-User-Id")] Guid userId)
    {
        var userWorkspaces = await workspaceService.ListForUserAsync(userId);
        return Ok(userWorkspaces);
    }
}