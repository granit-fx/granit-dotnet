using Granit.Users;
using Granit.UserSessions.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.UserSessions.Endpoints.Endpoints;

/// <summary>
/// The canonical <c>/devices</c> endpoint: lists the caller's devices.
/// </summary>
internal static class UserDeviceEndpoints
{
    internal static RouteGroupBuilder MapUserDeviceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .WithName("ListUserDevices")
            .WithSummary("Lists the caller's devices.")
            .WithDescription(
                "Returns the devices the authenticated user has signed in from — device type, OS, browser, "
                + "last activity and approximate location — aggregated across the configured session backend.")
            .Produces<IReadOnlyList<UserDeviceResponse>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

#pragma warning disable GRAPI003 // Private handler — resolved via the route delegate, not exposed as a parameter
    private static async Task<Results<Ok<IReadOnlyList<UserDeviceResponse>>, ProblemHttpResult>> ListAsync(
        [FromServices] IUserSessionManager manager,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
        {
            return TypedResults.Problem(
                detail: "The request is not associated with a user subject.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        IReadOnlyList<UserDevice> devices = await manager
            .ListDevicesAsync(currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<UserDeviceResponse> response = [.. devices.Select(Map)];
        return TypedResults.Ok(response);
    }
#pragma warning restore GRAPI003

    private static UserDeviceResponse Map(UserDevice device) =>
        new(device.DeviceId, device.Kind, device.DisplayName, device.OperatingSystem, device.Browser,
            device.LastSeen, device.SessionCount, device.LastLocation);
}
