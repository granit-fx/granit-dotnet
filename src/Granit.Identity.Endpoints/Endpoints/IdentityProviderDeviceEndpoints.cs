using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Admin read endpoint for inspecting another user's devices, served by the canonical
/// <see cref="IUserSessionManager"/> (geolocation applied once, no raw IP exposed).
/// </summary>
internal static class IdentityProviderDeviceEndpoints
{
    internal static RouteGroupBuilder MapProviderDeviceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetUserDevicesAsync)
            .WithName("GetIdentityProviderUserDevices")
            .WithSummary("Lists device activity for a user.")
            .WithDescription("Returns the devices the specified user has signed in from — device type, OS, browser, last activity and approximate location.")
            .Produces<IReadOnlyList<UserDeviceResponse>>();

        return group;
    }

    private static async Task<Ok<IReadOnlyList<UserDeviceResponse>>> GetUserDevicesAsync(
        string userId,
        [FromServices] IUserSessionManager sessionManager,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<UserDevice> devices = await sessionManager
            .ListDevicesAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<UserDeviceResponse> response = [.. devices.Select(IdentityResponseMapper.ToResponse)];
        return TypedResults.Ok(response);
    }
}
