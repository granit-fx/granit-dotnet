using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Endpoints.Options;
using Granit.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Endpoints.Endpoints;

/// <summary>
/// The canonical <c>/devices</c> endpoints: lists the caller's devices and manages device trust.
/// </summary>
internal static class MyUserDeviceEndpoints
{
    internal static RouteGroupBuilder MapMyUserDeviceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .WithName("ListMyUserDevices")
            .WithSummary("Lists the caller's devices.")
            .WithDescription(
                "Returns the devices the authenticated user has signed in from — device type, OS, browser, "
                + "last activity and approximate location — aggregated across the configured session backend.")
            .Produces<IReadOnlyList<UserDeviceResponse>>();

        group.MapPost("/trust", TrustAsync)
            .WithName("TrustMyDevice")
            .WithSummary("Marks the caller's current device as trusted.")
            .WithDescription(
                "Binds the current browser to a stable device identity (signed cookie) and records a trust "
                + "verdict for the configured duration. A trusted device may, when the deployment opts in, skip "
                + "the two-factor step-up on subsequent logins.")
            .Produces<DeviceTrustedResponse>();

        group.MapDelete("/{deviceId}/trust", RevokeTrustAsync)
            .WithName("RevokeMyDeviceTrust")
            .WithSummary("Revokes trust for one of the caller's devices.")
            .WithDescription(
                "Removes the trust verdict for the given device id. When it is the current device, the signed "
                + "device-trust cookie is also cleared so the device is no longer recognised.")
            .Produces(StatusCodes.Status204NoContent);

        return group;
    }

#pragma warning disable GRAPI003 // Private handlers — resolved via the route delegate, not exposed as a parameter
    private static async Task<Results<Ok<IReadOnlyList<UserDeviceResponse>>, ProblemHttpResult>> ListAsync(
        [FromServices] IUserSessionManager manager,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
        {
            return NotAUser();
        }

        IReadOnlyList<UserDevice> devices = await manager
            .ListDevicesAsync(currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<UserDeviceResponse> response = [.. devices.Select(IdentityResponseMapper.ToResponse)];
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<DeviceTrustedResponse>, ProblemHttpResult>> TrustAsync(
        HttpContext httpContext,
        [FromServices] IDeviceTrustCookieService cookieService,
        [FromServices] IIdentitySecurityStateStore securityState,
        [FromServices] ICurrentUserService currentUser,
        [FromServices] IOptions<DeviceTrustOptions> options,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
        {
            return NotAUser();
        }

        string deviceId = await cookieService.IssueDeviceCookieAsync(httpContext, currentUser.UserId)
            .ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset trustedUntil = now + options.Value.TrustDuration;

        await securityState.SetDeviceTrustAsync(
            currentUser.UserId,
            deviceId,
            new DeviceTrustVerdict(DeviceTrustLevel.Remembered, now, trustedUntil, "user_marked"),
            cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(new DeviceTrustedResponse(deviceId, trustedUntil));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RevokeTrustAsync(
        string deviceId,
        HttpContext httpContext,
        [FromServices] IDeviceTrustCookieService cookieService,
        [FromServices] IIdentitySecurityStateStore securityState,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currentUser.UserId))
        {
            return NotAUser();
        }

        await securityState.RevokeDeviceTrustAsync(currentUser.UserId, deviceId, cancellationToken).ConfigureAwait(false);

        if (string.Equals(cookieService.ResolveDeviceId(httpContext, currentUser.UserId), deviceId, StringComparison.Ordinal))
        {
            cookieService.Clear(httpContext);
        }

        return TypedResults.NoContent();
    }

    private static ProblemHttpResult NotAUser() =>
        TypedResults.Problem(
            detail: "The request is not associated with a user subject.",
            statusCode: StatusCodes.Status401Unauthorized);
#pragma warning restore GRAPI003
}
