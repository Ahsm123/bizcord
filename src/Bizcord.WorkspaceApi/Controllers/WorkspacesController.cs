using Bizcord.WorkspaceApi.Extensions;
using Bizcord.WorkspaceApi.Models;
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
        var dto = workspace.ToDto();
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

        var dto = workspace.ToDto();
        return Ok(dto);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WorkspaceDto>>> GetAll([FromHeader(Name = "X-User-Id")] Guid userId,
        CancellationToken ct)
    {
        var userWorkspaces = await workspaceService.GetAllAsync(userId, ct);
        return Ok(userWorkspaces);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Update([FromHeader(Name = "X-User-Id")] Guid userId, Guid id,
        UpdateWorkspaceRequest request, CancellationToken ct)
    {
        var result = await workspaceService.UpdateAsync(userId, id, request.Name, ct);
        if (result.IsSuccess)
        {
            return NoContent();
        }

        if (result.Error == WorkspaceErrors.NotFound)
        {
            return NotFound();
        }

        return StatusCode(StatusCodes.Status403Forbidden);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Delete([FromHeader(Name = "X-User-Id")] Guid userId, Guid id, 
        CancellationToken ct)
    {
        var result = await workspaceService.DeleteAsync(userId, id, ct);
        if (result.IsSuccess)
        {
            return NoContent();
        }

        
        if (result.Error == WorkspaceErrors.NotFound)
        {
            return NotFound();
        }

        return StatusCode(StatusCodes.Status403Forbidden);
    }
}