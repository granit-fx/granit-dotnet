using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Granit.Http.Resilience.Handlers;

/// <summary>
/// Propagates the incoming <c>Authorization</c> header to outgoing HTTP requests,
/// enabling transparent token forwarding for inter-service communication.
/// </summary>
/// <remarks>
/// The handler only forwards the token when the outgoing request does not already
/// contain an <c>Authorization</c> header, allowing explicit overrides when needed.
/// </remarks>
internal sealed class AuthTokenPropagationHandler(
    IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains("Authorization"))
        {
            HttpContext? context = httpContextAccessor.HttpContext;
            if (context is not null)
            {
                StringValues authHeader = context.Request.Headers.Authorization;
                if (!StringValues.IsNullOrEmpty(authHeader))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", (string?)authHeader);
                }
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
