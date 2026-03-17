using System.Security.Claims;
using Granit.Core.MultiTenancy;
using Granit.Notifications.MobilePush;
using Granit.Timing;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Granit.Notifications.Endpoints.Endpoints;

/// <summary>
/// Minimal API endpoints for mobile push device token management.
/// </summary>
public static class MobilePushTokenEndpoints
{
    /// <summary>Maps mobile push token management endpoints.</summary>
    public static IEndpointRouteBuilder MapMobilePushTokenEndpoints(
        this IEndpointRouteBuilder endpoints,
        string prefix = "api/notifications/mobile-push/tokens")
    {
        RouteGroupBuilder group = endpoints.MapGranitGroup(prefix)
            .RequireAuthorization()
            .WithTags("MobilePush");

        group.MapPost("/", RegisterTokenAsync)
            .WithName("RegisterMobilePushToken")
            .WithSummary("Registers a mobile device token for push notifications.")
            .WithDescription("Registers a device token (FCM or APNs) for the authenticated user. If the token already exists, it is updated (upsert). Returns 201 Created for new registrations, 200 OK for updates. Tokens are scoped to the current tenant.");

        group.MapDelete("/{deviceToken}", RemoveTokenAsync)
            .WithName("RemoveMobilePushToken")
            .WithSummary("Removes a mobile device token.")
            .WithDescription("Removes the specified device token for the current tenant. Call this when the user logs out or the token becomes invalid. No-op if the token does not exist.");

        group.MapGet("/", GetTokensAsync)
            .WithName("GetMobilePushTokens")
            .WithSummary("Returns the current user's registered device tokens.")
            .WithDescription("Returns all device tokens registered by the authenticated user for the current tenant, including the platform (iOS, Android) and registration timestamp.");

        return endpoints;
    }

    private static async Task<Results<Created, Ok>> RegisterTokenAsync(
        MobilePushTokenRegisterRequest request,
        IMobilePushTokenWriter tokenWriter,
        IMobilePushTokenReader tokenReader,
        ClaimsPrincipal user,
        ICurrentTenant tenant,
        IClock clock,
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
        IMobilePushTokenWriter tokenWriter,
        ICurrentTenant tenant,
        CancellationToken cancellationToken)
    {
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;

        await tokenWriter.RemoveAsync(deviceToken, tenantId, cancellationToken).ConfigureAwait(false);

        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<MobilePushTokenResponse>>> GetTokensAsync(
        IMobilePushTokenReader tokenReader,
        ClaimsPrincipal user,
        ICurrentTenant tenant,
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

/// <summary>Request to register a mobile push device token.</summary>
public sealed record MobilePushTokenRegisterRequest
{
    /// <summary>Device token from FCM/APNs.</summary>
    public required string DeviceToken { get; init; }

    /// <summary>Device platform.</summary>
    public required MobilePlatform Platform { get; init; }
}

/// <summary>Response for a mobile push device token.</summary>
public sealed record MobilePushTokenResponse(string DeviceToken, MobilePlatform Platform, DateTimeOffset CreatedAt);
