using System.Security.Claims;
using Granit.Authorization;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Notifications.Endpoints.Dtos;
using Granit.Notifications.Endpoints.Internal;
using Granit.Notifications.Endpoints.Permissions;
using Granit.Timing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Notifications.Endpoints.Endpoints;

/// <summary>
/// Minimal API endpoints for notification delivery preferences and type discovery.
/// </summary>
internal static class PreferenceEndpoints
{
    /// <summary>Maps all preference and notification-type endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapPreferenceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/preferences", GetPreferencesAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Read)
            .WithName("GetPreferences")
            .WithSummary("Returns notification delivery preferences for the current user.")
            .WithDescription("Returns all notification delivery preferences for the authenticated user within the current tenant. Each preference indicates whether a specific notification type is enabled or disabled for a given channel.")
            .Produces<List<NotificationPreferenceResponse>>();

        group.MapPut("/preferences", UpdatePreferenceAsync)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Manage)
            .WithName("UpdatePreference")
            .WithSummary("Creates or updates a notification delivery preference.")
            .WithDescription("Creates or updates a delivery preference for a specific notification type and channel. If a preference already exists for the same type and channel, it is replaced (upsert).")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

        group.MapGet("/types", GetNotificationTypes)
            .RequireAuthorization(NotificationPermissions.UserNotifications.Read)
            .WithName("GetNotificationTypes")
            .WithSummary("Returns all registered notification type definitions.")
            .WithDescription("Returns all notification types registered in the system with their metadata. Use this to build the preferences UI, showing which notification types are available and their supported channels.")
            .Produces<IReadOnlyList<NotificationDefinition>>();

        return group;
    }

    private static async Task<Ok<List<NotificationPreferenceResponse>>> GetPreferencesAsync(
        [FromServices] INotificationPreferenceReader reader,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user)
    {
        string userId = NotificationsResponseMapper.GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        IReadOnlyList<NotificationPreference> preferences = await reader.GetListAsync(userId, tenantId).ConfigureAwait(false);
        var result = preferences
            .Select(p => new NotificationPreferenceResponse(p.Id, p.UserId, p.NotificationTypeName, p.ChannelName, p.IsEnabled))
            .ToList();
        return TypedResults.Ok(result);
    }

    private static async Task<NoContent> UpdatePreferenceAsync(
        NotificationPreferenceUpdateRequest request,
        [FromServices] INotificationPreferenceWriter writer,
        [FromServices] IGuidGenerator guidGenerator,
        [FromServices] ICurrentTenant tenant,
        ClaimsPrincipal user,
        [FromServices] IClock clock)
    {
        string userId = NotificationsResponseMapper.GetUserId(user);
        Guid? tenantId = tenant.IsAvailable ? tenant.Id : null;
        NotificationPreference preference = new()
        {
            Id = guidGenerator.Create(),
            UserId = userId,
            NotificationTypeName = request.NotificationTypeName,
            ChannelName = request.ChannelName,
            IsEnabled = request.IsEnabled,
            TenantId = tenantId,
            CreatedAt = clock.Now,
            CreatedBy = userId,
            ModifiedAt = clock.Now,
            ModifiedBy = userId,
        };
        await writer.SetAsync(preference).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<NotificationDefinition>>> GetNotificationTypes(
        [FromServices] INotificationDefinitionStore definitionStore,
        [FromServices] IPermissionChecker permissionChecker,
        [FromServices] IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<NotificationDefinition> definitions = definitionStore.GetAll();
        if (definitions.Count == 0)
        {
            return TypedResults.Ok(definitions);
        }

        IReadOnlyList<NotificationDefinition> filtered = await FilterAsync(
            definitions, permissionChecker, serviceProvider, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok(filtered);
    }

    private static async Task<IReadOnlyList<NotificationDefinition>> FilterAsync(
        IReadOnlyList<NotificationDefinition> definitions,
        IPermissionChecker permissionChecker,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        HashSet<string> grantedPermissions = await ResolveGrantedPermissionsAsync(
            definitions, permissionChecker, cancellationToken).ConfigureAwait(false);

        var featureGate = (INotificationFeatureGate?)serviceProvider.GetService(typeof(INotificationFeatureGate));
        HashSet<string> enabledFeatures = await ResolveEnabledFeaturesAsync(
            definitions, featureGate, cancellationToken).ConfigureAwait(false);

        List<NotificationDefinition> result = new(definitions.Count);
        foreach (NotificationDefinition def in definitions)
        {
            if (def.RequiredPermission is { Length: > 0 } perm
                && !grantedPermissions.Contains(perm))
            {
                continue;
            }

            if (def.RequiredFeature is { Length: > 0 } feat
                && featureGate is not null
                && !enabledFeatures.Contains(feat))
            {
                continue;
            }

            result.Add(def);
        }

        return result;
    }

    private static async Task<HashSet<string>> ResolveGrantedPermissionsAsync(
        IReadOnlyList<NotificationDefinition> definitions,
        IPermissionChecker permissionChecker,
        CancellationToken cancellationToken)
    {
        HashSet<string> requested = new(StringComparer.Ordinal);
        foreach (NotificationDefinition def in definitions)
        {
            if (def.RequiredPermission is { Length: > 0 } perm)
            {
                requested.Add(perm);
            }
        }

        if (requested.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        try
        {
            IReadOnlyList<string> granted = await permissionChecker.GetGrantedAsync(
                [.. requested], cancellationToken).ConfigureAwait(false);
            return new HashSet<string>(granted, StringComparer.Ordinal);
        }
        catch (InvalidOperationException)
        {
            // One of the referenced permissions wasn't declared in the host (typically because
            // the corresponding *.Endpoints module isn't mounted). Treat all of them as not
            // granted so the unfit notifications stay hidden from the preferences UI.
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    private static async Task<HashSet<string>> ResolveEnabledFeaturesAsync(
        IReadOnlyList<NotificationDefinition> definitions,
        INotificationFeatureGate? featureGate,
        CancellationToken cancellationToken)
    {
        HashSet<string> enabled = new(StringComparer.Ordinal);
        if (featureGate is null)
        {
            return enabled;
        }

        HashSet<string> requested = new(StringComparer.Ordinal);
        foreach (NotificationDefinition def in definitions)
        {
            if (def.RequiredFeature is { Length: > 0 } feat)
            {
                requested.Add(feat);
            }
        }

        foreach (string feature in requested)
        {
            try
            {
                if (await featureGate.IsFeatureEnabledAsync(feature, cancellationToken).ConfigureAwait(false))
                {
                    enabled.Add(feature);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Safe default: any other failure of a host-supplied gate keeps the notification hidden
                // rather than 500-ing the preferences endpoint. The gate impl is third-party from the
                // framework's perspective, so we can't narrow to a known exception type.
            }
        }

        return enabled;
    }
}
