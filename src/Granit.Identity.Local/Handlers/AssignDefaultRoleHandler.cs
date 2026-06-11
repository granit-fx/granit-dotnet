using Granit.Identity.Local.Events;
using Granit.Identity.Models;
using Granit.MultiTenancy;
using Granit.Settings.Services;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Local.Handlers;

/// <summary>
/// Assigns the configured default role (<see cref="IdentityLocalSettingNames.DefaultUserRole"/>)
/// to every newly self-registered user. Subscribes to <see cref="UserRegisteredEto"/>, which is
/// published by BOTH the local registration endpoint and the external-provider registration flow,
/// so the two paths provision roles symmetrically.
/// </summary>
/// <remarks>
/// Opt-in: when the setting is empty (the default) no role is assigned. The assignment is
/// idempotent and non-blocking — a role that does not exist, or a user already in the role, is
/// logged and skipped rather than thrown, so a misconfigured setting never poisons the Wolverine
/// outbox nor blocks the registration side effects (welcome notification, session). A user whose
/// configured role is missing simply receives no role (secure-by-default).
/// </remarks>
public partial class AssignDefaultRoleHandler
{
    public static async Task HandleAsync(
        UserRegisteredEto evt,
        ISettingProvider settingProvider,
        IIdentityRoleManager roleManager,
        ICurrentTenant currentTenant,
        ILogger<AssignDefaultRoleHandler> logger,
        CancellationToken cancellationToken)
    {
        // Resolve the setting and assign the role within the user's tenant scope so a per-tenant
        // override wins and the role lookup runs in the correct tenant. evt.TenantId is null for
        // host/local registration.
        using IDisposable? tenantScope = currentTenant.Change(evt.TenantId);

        string? roleName = await settingProvider
            .GetOrNullAsync(IdentityLocalSettingNames.DefaultUserRole, cancellationToken)
            .ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(roleName))
        {
            return;
        }

        string userId = evt.UserId.ToString();

        // Idempotency: skip without throwing if the user already holds the role (handler retries,
        // duplicate delivery). Avoids relying on exception-message parsing for the common path.
        IReadOnlyList<IdentityRole> currentRoles = await roleManager
            .GetUserRolesAsync(userId, cancellationToken).ConfigureAwait(false);

        if (currentRoles.Any(r => string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        try
        {
            await roleManager.AssignRoleAsync(userId, roleName, cancellationToken).ConfigureAwait(false);
            LogDefaultRoleAssigned(logger, roleName, evt.UserId);
        }
        catch (InvalidOperationException ex)
        {
            // Most likely the role does not exist (no seed). Non-blocking by design: the user is
            // created without a role rather than failing registration.
            LogDefaultRoleSkipped(logger, roleName, evt.UserId, ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Assigned default role '{RoleName}' to newly registered user {UserId}.")]
    private static partial void LogDefaultRoleAssigned(ILogger logger, string roleName, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Default role '{RoleName}' could not be assigned to user {UserId} — registration succeeded but no role was assigned. {Reason}")]
    private static partial void LogDefaultRoleSkipped(ILogger logger, string roleName, Guid userId, string reason);
}
