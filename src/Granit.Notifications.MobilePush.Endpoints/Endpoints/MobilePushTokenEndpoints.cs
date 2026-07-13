using System.Security.Claims;
using Granit.MultiTenancy;
using Granit.Notifications.Endpoints.Internal;
using Granit.Notifications.Endpoints.Permissions;
using Granit.Notifications.MobilePush.Domain;
using Granit.Notifications.MobilePush.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Notifications.MobilePush.Endpoints.Endpoints;

/// <summary>
/// Minimal API endpoints for mobile push device token management.
/// </summary>
internal static class MobilePushTokenEndpoints
{
    /// <summary>Maps the mobile push token management endpoints onto the given route group.</summary>
    public static RouteGroupBuilder MapMobilePushTokenEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/tokens", RegisterTokenAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Manage)
            .WithName("RegisterMobilePushToken")
            .WithSummary("Registers a mobile device token for push notifications.")
            .WithDescription("Registers a device token (FCM or APNs) for the authenticated user. If the token already exists, it is updated (upsert). Returns 201 Created for new registrations, 200 OK for updates. Tokens are scoped to the current tenant.")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/tokens", RemoveTokenAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Manage)
            .WithName("RemoveMobilePushToken")
            .WithSummary("Removes a mobile device token.")
            .WithDescription("Removes the device token carried in the request body for the authenticated user in the current tenant. The token is a sendable push credential, so it travels in the body — never in the URL, where it would leak into access and proxy logs. Call this when the user logs out or the token becomes invalid. No-op if the token does not exist.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/tokens", GetTokensAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Read)
            .WithName("GetMobilePushTokens")
            .WithSummary("Returns the current user's registered device tokens.")
            .WithDescription("Returns all device tokens registered by the authenticated user for the current tenant, including the platform (iOS, Android) and registration timestamp. The device token itself is a sendable push credential, so only a masked preview (last 4 characters) is returned — never the plaintext token.")
            .Produces<IReadOnlyList<MobilePushTokenResponse>>();

        return group;
    }

    private static async Task<Results<Created, Ok>> RegisterTokenAsync(
        MobilePushTokenRegisterRequest request,
        [FromServices] IMobilePushTokenWriter tokenWriter,
        [FromServices] IMobilePushTokenReader tokenReader,
        ClaimsPrincipal user,
        [FromServices] ICurrentTenant tenant,
        CancellationToken cancellationToken)
    {
        string userId = NotificationsResponseMapper.GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;

        // Check if token already exists (upsert) — equality is on the encrypted
        // plaintext at the abstraction level; the store routes the comparison
        // through the lookup hash internally.
        IReadOnlyList<MobilePushToken> existing = await tokenReader
            .GetTokensAsync(userId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        bool isUpdate = existing.Any(t => t.DeviceToken == request.DeviceToken);

        await tokenWriter
            .RegisterAsync(userId, request.DeviceToken, request.Platform, tenantId, cancellationToken)
            .ConfigureAwait(false);

        return isUpdate
            ? TypedResults.Ok()
            : TypedResults.Created();
    }

    private static async Task<NoContent> RemoveTokenAsync(
        [FromBody] MobilePushTokenRemoveRequest request,
        ClaimsPrincipal user,
        [FromServices] IMobilePushTokenWriter tokenWriter,
        [FromServices] ICurrentTenant tenant,
        CancellationToken cancellationToken)
    {
        string userId = NotificationsResponseMapper.GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;

        await tokenWriter.RemoveAsync(request.DeviceToken, userId, tenantId, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<MobilePushTokenResponse>>> GetTokensAsync(
        [FromServices] IMobilePushTokenReader tokenReader,
        ClaimsPrincipal user,
        [FromServices] ICurrentTenant tenant,
        CancellationToken cancellationToken)
    {
        string userId = NotificationsResponseMapper.GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;

        IReadOnlyList<MobilePushToken> tokens = await tokenReader
            .GetTokensAsync(userId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<MobilePushTokenResponse> response = tokens
            .Select(t => new MobilePushTokenResponse(MaskDeviceToken(t.DeviceToken), t.Platform, t.CreatedAt))
            .ToList();

        return TypedResults.Ok(response);
    }

    // The device token is a sendable push credential (stored encrypted, keyed by a lookup hash), so
    // the list endpoint never echoes the plaintext. Deletion uses the token the client already holds
    // from the FCM/APNs SDK, so a masked preview suffices to disambiguate devices in a management UI.
    private static string MaskDeviceToken(string deviceToken) =>
        deviceToken.Length <= 4
            ? "…"
            : $"…{deviceToken.AsSpan(deviceToken.Length - 4)}";
}
