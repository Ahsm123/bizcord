using System.ComponentModel.DataAnnotations;

namespace Bizcord.WorkspaceApi.Requests;

public record CreateWorkspaceRequest([Required(AllowEmptyStrings = false), MaxLength(100)] string Name);
