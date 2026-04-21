using System.Security.Claims;
using Granit.MultiTenancy;
using Granit.Notifications.Endpoints.Dtos;
using Granit.Notifications.Endpoints.Options;
using Granit.Notifications.Endpoints.Permissions;
using Granit.Notifications.MobilePush;
using Granit.Timing;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Notifications.Endpoints.Endpoints;

/// <summary>
/// Minimal API endpoints for mobile push device token management.
/// </summary>
public static class MobilePushTokenEndpoints
{
    /// <summary>Maps mobile push token management endpoints.</summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="prefix">Route prefix. Default <c>"api/notifications/mobile-push/tokens"</c>.</param>
    /// <param name="configure">Optional delegate to customize <see cref="NotificationEndpointsOptions"/> (tag only).</param>
    public static IEndpointRouteBuilder MapGranitMobilePushTokens(
        this IEndpointRouteBuilder endpoints,
        string prefix = "api/notifications/mobile-push/tokens",
        Action<NotificationEndpointsOptions>? configure = null)
    {
        NotificationEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints.MapGranitGroup(prefix)
            .RequireAuthorization()
            .WithTags(options.MobilePushTagName);

        group.MapPost("/", RegisterTokenAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Manage)
            .WithName("RegisterMobilePushToken")
            .WithSummary("Registers a mobile device token for push notifications.")
            .WithDescription("Registers a device token (FCM or APNs) for the authenticated user. If the token already exists, it is updated (upsert). Returns 201 Created for new registrations, 200 OK for updates. Tokens are scoped to the current tenant.")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapDelete("/{deviceToken}", RemoveTokenAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Manage)
            .WithName("RemoveMobilePushToken")
            .WithSummary("Removes a mobile device token.")
            .WithDescription("Removes the specified device token for the authenticated user in the current tenant. Call this when the user logs out or the token becomes invalid. No-op if the token does not exist.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/", GetTokensAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Read)
            .WithName("GetMobilePushTokens")
            .WithSummary("Returns the current user's registered device tokens.")
            .WithDescription("Returns all device tokens registered by the authenticated user for the current tenant, including the platform (iOS, Android) and registration timestamp.")
            .Produces<IReadOnlyList<MobilePushTokenResponse>>();

        return endpoints;
    }

    private static async Task<Results<Created, Ok>> RegisterTokenAsync(
        MobilePushTokenRegisterRequest request,
        [FromServices] IMobilePushTokenWriter tokenWriter,
        [FromServices] IMobilePushTokenReader tokenReader,
        ClaimsPrincipal user,
        [FromServices] ICurrentTenant tenant,
        [FromServices] IClock clock,
        CancellationToken cancellationToken)
    {
        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;

        // Check if token already exists (upsert)
        IReadOnlyList<MobilePushTokenInfo> existing = await tokenReader
            .GetTokensAsync(userId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        bool isUpdate = existing.Any(t => t.DeviceToken == request.DeviceToken);

        await tokenWriter.RegisterAsync(new MobilePushTokenInfo
        {
            UserId = userId,
            DeviceToken = request.DeviceToken,
            Platform = request.Platform,
            TenantId = tenantId,
            CreatedAt = clock.Now,
        }, cancellationToken).ConfigureAwait(false);

        return isUpdate
            ? TypedResults.Ok()
            : TypedResults.Created();
    }

    private static async Task<NoContent> RemoveTokenAsync(
        string deviceToken,
        ClaimsPrincipal user,
        [FromServices] IMobilePushTokenWriter tokenWriter,
        [FromServices] ICurrentTenant tenant,
        CancellationToken cancellationToken)
    {
        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;

        await tokenWriter.RemoveAsync(deviceToken, userId, tenantId, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<MobilePushTokenResponse>>> GetTokensAsync(
        [FromServices] IMobilePushTokenReader tokenReader,
        ClaimsPrincipal user,
        [FromServices] ICurrentTenant tenant,
        CancellationToken cancellationToken)
    {
        string userId = GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;

        IReadOnlyList<MobilePushTokenInfo> tokens = await tokenReader
            .GetTokensAsync(userId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<MobilePushTokenResponse> response = tokens
            .Select(t => new MobilePushTokenResponse(t.DeviceToken, t.Platform, t.CreatedAt))
            .ToList();

        return TypedResults.Ok(response);
    }

    private static string GetUserId(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("sub")
        ?? throw new InvalidOperationException("User identifier claim not found.");
}
