using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Granit.OpenIddict.Endpoints.Endpoints;

/// <summary>
/// OIDC end-user verification endpoint (<c>GET/POST /connect/verify</c>).
/// Stub implementation — device authorization flow is not yet supported.
/// </summary>
internal static class ConnectVerifyEndpoints
{
    internal static IEndpointRouteBuilder MapConnectVerifyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods("/connect/verify", ["GET", "POST"],
                () => TypedResults.Problem(
                    detail: "Device authorization verification is not yet implemented.",
                    statusCode: StatusCodes.Status501NotImplemented))
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }
}
