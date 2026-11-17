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
    public async Task<ActionResult<WorkspaceDto>> Create(CreateWorkspaceRequest request, CancellationToken ct)
    {
        var workspace = await workspaceService.CreateAsync(User.GetUserId(), request.Name, ct);
        var dto = workspace.ToDto();
        return CreatedAtAction(nameof(GetById), new { id = workspace.Id }, dto);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]                 
    [ProducesResponseType(StatusCodes.Status403Forbidden)]                  
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkspaceDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await workspaceService.GetByIdAsync(id, User.GetUserId(), ct);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        return Ok(result.Value.ToDto());
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<WorkspaceDto>>> GetAll(CancellationToken ct)
    {
        var userWorkspaces = await workspaceService.GetAllAsync(User.GetUserId(), ct);
        return Ok(userWorkspaces);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Update(Guid id, UpdateWorkspaceRequest request, CancellationToken ct)
    {
        var result = await workspaceService.UpdateAsync(User.GetUserId(), id, request.Name, ct);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await workspaceService.DeleteAsync(User.GetUserId(), id, ct);
        return result.ToActionResult();
    }
}