using System.Security.Claims;
using Granit.MultiTenancy;
using Granit.Notifications.Endpoints.Internal;
using Granit.Notifications.Endpoints.Permissions;
using Granit.Notifications.WebPush.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Notifications.WebPush.Endpoints.Endpoints;

/// <summary>
/// Minimal API endpoints for browser Web Push (W3C, VAPID) subscription management.
/// </summary>
internal static class WebPushSubscriptionEndpoints
{
    /// <summary>Maps the Web Push subscription endpoints onto the given route group.</summary>
    public static RouteGroupBuilder MapWebPushSubscriptionEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/push/subscriptions", RegisterSubscriptionAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Manage)
            .WithName("RegisterWebPushSubscription")
            .WithSummary("Registers a browser Web Push subscription.")
            .WithDescription("Registers a W3C Web Push subscription (endpoint + encryption keys) for the authenticated user. If the endpoint is already registered, it is updated (upsert). Returns 201 Created for new subscriptions, 200 OK for updates. Subscriptions are scoped to the current tenant.")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/push/subscriptions", RemoveSubscriptionAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Manage)
            .WithName("RemoveWebPushSubscription")
            .WithSummary("Removes a browser Web Push subscription.")
            .WithDescription("Removes the Web Push subscription identified by its endpoint. The endpoint travels in the request body because it is an opaque, slash-bearing URL unsuitable as a route segment. Call this when the user disables push or the browser rotates the subscription. No-op if the endpoint is unknown.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);

        return group;
    }

    private static async Task<Results<Created, Ok>> RegisterSubscriptionAsync(
        WebPushSubscriptionRegisterRequest request,
        [FromServices] IPushSubscriptionWriter writer,
        [FromServices] IPushSubscriptionReader reader,
        ClaimsPrincipal user,
        [FromServices] ICurrentTenant tenant,
        CancellationToken cancellationToken)
    {
        string userId = NotificationsResponseMapper.GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;

        IReadOnlyList<PushSubscriptionInfo> existing = await reader
            .GetSubscriptionsAsync(userId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        bool isUpdate = existing.Any(s => s.Endpoint == request.Endpoint);

        PushSubscriptionInfo subscription = new()
        {
            Endpoint = request.Endpoint,
            ExpirationTime = request.ExpirationTime,
            P256dh = request.Keys.P256dh,
            Auth = request.Keys.Auth,
        };

        await writer.SaveSubscriptionAsync(userId, subscription, tenantId, cancellationToken).ConfigureAwait(false);

        return isUpdate
            ? TypedResults.Ok()
            : TypedResults.Created();
    }

    private static async Task<NoContent> RemoveSubscriptionAsync(
        [FromBody] WebPushSubscriptionRemoveRequest request,
        [FromServices] IPushSubscriptionWriter writer,
        [FromServices] ICurrentTenant tenant,
        CancellationToken cancellationToken)
    {
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;

        // The removal seam is scoped by (endpoint, tenant), not by user: the push endpoint is a
        // cryptographically-unguessable secret minted by the browser's push service, so an
        // attacker cannot target another user's subscription without already knowing it. A
        // user-scoped overload of IPushSubscriptionWriter.RemoveSubscriptionAsync is a follow-up.
        await writer.RemoveSubscriptionAsync(request.Endpoint, tenantId, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }
}
