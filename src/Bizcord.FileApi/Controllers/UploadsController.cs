using Bizcord.Contracts.Files;
using Bizcord.FileApi.Extensions;
using Bizcord.FileApi.Requests;
using Bizcord.FileApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bizcord.FileApi.Controllers;

[ApiController]
[Route("api/v1/uploads")]
public class UploadsController(IUploadService uploadService) : ControllerBase
{
    // 202, because the upload is only accepted. The saga decides later if it is Confirmed or Rejected,
    // and the client follows it with GET on the Location.
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UploadDto>> Create(CreateUploadRequest request, CancellationToken ct)
    {
        var upload = await uploadService.StartAsync(User.GetUserId(), request, ct);
        return AcceptedAtAction(nameof(GetById), new { id = upload.Id }, upload.ToDto());
    }

    // 404 both when the upload doesn't exist and when it belongs to another user
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UploadDto>> GetById(Guid id, CancellationToken ct)
    {
        var upload = await uploadService.GetForUploaderAsync(User.GetUserId(), id, ct);
        return upload is null ? NotFound() : Ok(upload.ToDto());
    }
}
