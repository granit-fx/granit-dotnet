using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Endpoints.Options;
using Granit.Identity.Models;
using Granit.IpGeolocation;
using Granit.UserSessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// Read endpoint for inspecting device activity for a user.
/// </summary>
internal static class IdentityProviderDeviceEndpoints
{
    internal static RouteGroupBuilder MapProviderDeviceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetUserDeviceActivityAsync)
            .WithName("GetIdentityProviderUserDevices")
            .WithSummary("Lists device activity for a user.")
            .WithDescription("Returns device activity information for the specified user, including device type, OS, browser, and associated sessions.")
            .Produces<IReadOnlyList<IdentityDeviceActivityResponse>>();

        return group;
    }

    private static async Task<Ok<IReadOnlyList<IdentityDeviceActivityResponse>>> GetUserDeviceActivityAsync(
        string userId,
        [FromServices] IIdentitySessionManager sessionManager,
        [FromServices] IIpGeolocationResolver geoResolver,
        [FromServices] IUserSessionRiskStore riskStore,
        [FromServices] IOptions<IdentityEndpointsOptions> options,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IdentityDeviceActivity> devices = await sessionManager
            .GetUserDeviceActivityAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        List<IdentityDeviceActivityResponse> enriched = await IdentitySessionEnrichment
            .EnrichDevicesAsync(devices, userId, geoResolver, riskStore, options.Value.ExposeRawIpAddress, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok<IReadOnlyList<IdentityDeviceActivityResponse>>(enriched);
    }
}
