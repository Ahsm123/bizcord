using System.ComponentModel.DataAnnotations;

namespace Bizcord.WorkspaceApi.Requests;

public record UpdateWorkspaceRequest([Required(AllowEmptyStrings = false), MaxLength(100)] string Name);