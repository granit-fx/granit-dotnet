using Microsoft.AspNetCore.Http;

namespace Granit.Authentication.JwtBearer.BackChannelLogout;

/// <summary>
/// Endpoint filter that rejects requests without <c>application/x-www-form-urlencoded</c> content type.
/// Applied to the back-channel logout endpoint to enforce the OIDC specification requirement
/// before the handler runs.
/// </summary>
internal sealed class FormContentTypeEndpointFilter : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        if (!context.HttpContext.Request.HasFormContentType)
        {
            return ValueTask.FromResult<object?>(TypedResults.Problem(
                detail: "Expected application/x-www-form-urlencoded content type.",
                statusCode: StatusCodes.Status400BadRequest));
        }

        return next(context);
    }
}
