using Bizcord.WorkspaceApi.Extensions;
using HotChocolate;
using DomainError = Bizcord.WorkspaceApi.Models.Error;

namespace Bizcord.WorkspaceApi.GraphQL;

// A failed Result becomes a GraphQL error instead of ProblemDetails. The code is the same as the REST title,
// and the status extension is the status code the REST endpoint would have returned.
public static class GraphQLErrors
{
    public const string ValidationCode = "Workspace.Validation";

    public static GraphQLException From(DomainError error) =>
        new(ErrorBuilder.New()
            .SetMessage(error.Description)
            .SetCode(error.Code)
            .SetExtension("status", error.ToStatusCode())
            .Build());

    public static GraphQLException Invalid(string message) =>
        new(ErrorBuilder.New()
            .SetMessage(message)
            .SetCode(ValidationCode)
            .SetExtension("status", StatusCodes.Status400BadRequest)
            .Build());

    // Same rule as the REST requests: 1 to 100 characters
    public static string ValidName(string name, string what) =>
        string.IsNullOrWhiteSpace(name) || name.Length > 100
            ? throw Invalid($"The {what} name must be 1 to 100 characters")
            : name;
}
