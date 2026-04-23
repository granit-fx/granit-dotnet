using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

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
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IdentityDeviceActivity> devices = await sessionManager
            .GetUserDeviceActivityAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok<IReadOnlyList<IdentityDeviceActivityResponse>>(
            devices.Select(IdentityResponseMapper.ToResponse).ToList());
    }
}
