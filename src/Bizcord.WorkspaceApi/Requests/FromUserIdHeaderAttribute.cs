using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Bizcord.WorkspaceApi.Requests;

// Binds the acting user's id from the X-User-Id header the BFF sets.
// A missing header would otherwise bind to Guid.Empty, so that is rejected as well as an explicit empty Guid,
// and [ApiController] turns it into a 400.
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromUserIdHeaderAttribute()
    : ValidationAttribute("The X-User-Id header must be a non-empty Guid"), IBindingSourceMetadata, IModelNameProvider
{
    public BindingSource BindingSource => BindingSource.Header;
    public string Name => "X-User-Id";

    public override bool IsValid(object? value) => value is Guid id && id != Guid.Empty;
}
