using Granit.Identity.Federated.Diagnostics;
using Granit.Identity.Federated.Exceptions;
using Granit.Identity.Models;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Federated.Internal;

/// <summary>
/// Decorates the registered <see cref="IIdentityProvider"/> with uniform, centralized
/// graceful degradation. Read operations that fail with a classified
/// <see cref="IdentityProviderException"/> return an empty/absent result (a blip does not
/// fail the whole request), while the failure is logged and metered by category. Two rules
/// are never relaxed:
/// <list type="bullet">
/// <item><description><see cref="OperationCanceledException"/> is always re-thrown — cancellation
/// (request abort, host shutdown) is never swallowed, closing the bug the per-provider
/// <c>catch (Exception)</c> blocks left open.</description></item>
/// <item><description><see cref="IdentityProviderFailureCategory.Unauthorized"/> is re-thrown, not
/// degraded — an IAM outage surfaces to the caller instead of masquerading as "no data".</description></item>
/// </list>
/// Write operations pass through unchanged: a failed mutation must always surface. Untyped
/// exceptions also propagate — the decorator only degrades the classified provider faults,
/// so genuine bugs are never hidden.
/// </summary>
internal sealed partial class GracefulIdentityProviderDecorator(
    IIdentityProvider inner,
    ILogger<GracefulIdentityProviderDecorator> logger,
    IdentityFederatedMetrics? metrics = null,
    ICurrentTenant? currentTenant = null) : IIdentityProvider
{
    // ──── IIdentityUserReader (degrade) ────

    public Task<IReadOnlyList<IIdentityUser>> GetUsersAsync(
        string? search = null, int? first = null, int? max = null, CancellationToken cancellationToken = default) =>
        DegradeAsync(() => inner.GetUsersAsync(search, first, max, cancellationToken), []);

    public Task<IIdentityUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default) =>
        DegradeAsync<IIdentityUser?>(() => inner.GetUserAsync(userId, cancellationToken), null);

    // ──── IIdentityUserWriter (pass-through) ────

    public Task SetUserEnabledAsync(string userId, bool enabled, CancellationToken cancellationToken = default) =>
        inner.SetUserEnabledAsync(userId, enabled, cancellationToken);

    public Task UpdateUserAsync(string userId, IdentityUserUpdate update, CancellationToken cancellationToken = default) =>
        inner.UpdateUserAsync(userId, update, cancellationToken);

    public Task<IIdentityUser> CreateUserAsync(IdentityUserCreate user, CancellationToken cancellationToken = default) =>
        inner.CreateUserAsync(user, cancellationToken);

    // ──── IIdentityRoleManager ────

    public Task<IReadOnlyList<IdentityRole>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        DegradeAsync(() => inner.GetRolesAsync(cancellationToken), []);

    public Task<IReadOnlyList<IIdentityUser>> GetRoleMembersAsync(string roleName, CancellationToken cancellationToken = default) =>
        DegradeAsync(() => inner.GetRoleMembersAsync(roleName, cancellationToken), []);

    public Task<IReadOnlyList<IdentityRole>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default) =>
        DegradeAsync(() => inner.GetUserRolesAsync(userId, cancellationToken), []);

    public Task AssignRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default) =>
        inner.AssignRoleAsync(userId, roleName, cancellationToken);

    public Task RemoveRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default) =>
        inner.RemoveRoleAsync(userId, roleName, cancellationToken);

    // NB: IIdentityClientRoleManager is NOT part of IIdentityProvider — it is an opt-in Phase 2
    // facet cast off the concrete provider by the sync pipeline (which classifies its own fetch
    // faults). It is therefore not decorated: provider registration forwards that facet, plus the
    // session/device facets, to the concrete provider rather than to this decorator.

    // ──── IIdentityGroupManager ────

    public Task<IReadOnlyList<IdentityGroup>> GetGroupsAsync(CancellationToken cancellationToken = default) =>
        DegradeAsync(() => inner.GetGroupsAsync(cancellationToken), []);

    public Task<IReadOnlyList<IdentityGroup>> GetUserGroupsAsync(string userId, CancellationToken cancellationToken = default) =>
        DegradeAsync(() => inner.GetUserGroupsAsync(userId, cancellationToken), []);

    public Task AddUserToGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default) =>
        inner.AddUserToGroupAsync(userId, groupId, cancellationToken);

    public Task RemoveUserFromGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default) =>
        inner.RemoveUserFromGroupAsync(userId, groupId, cancellationToken);

    // ──── IIdentityPasswordManager ────

    public Task<DateTimeOffset?> GetPasswordChangedAtAsync(string userId, CancellationToken cancellationToken = default) =>
        DegradeAsync<DateTimeOffset?>(() => inner.GetPasswordChangedAtAsync(userId, cancellationToken), null);

    public Task SendPasswordResetEmailAsync(string userId, CancellationToken cancellationToken = default) =>
        inner.SendPasswordResetEmailAsync(userId, cancellationToken);

    public Task SetTemporaryPasswordAsync(string userId, string temporaryPassword, CancellationToken cancellationToken = default) =>
        inner.SetTemporaryPasswordAsync(userId, temporaryPassword, cancellationToken);

    // ──── IIdentityCredentialVerifier (pass-through: never degrade an auth decision) ────

    public Task<bool> VerifyUserCredentialsAsync(string username, string password, CancellationToken cancellationToken = default) =>
        inner.VerifyUserCredentialsAsync(username, password, cancellationToken);

    // ──── degradation policy ────

    private async Task<T> DegradeAsync<T>(Func<Task<T>> operation, T degraded)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Never swallow cancellation — request abort / host shutdown must propagate.
            throw;
        }
        catch (IdentityProviderException ex)
        {
            metrics?.RecordProviderFailure(ex.ProviderName, ex.Operation, ex.Category, CurrentTenantId());

            if (ex.Category is IdentityProviderFailureCategory.Unauthorized)
            {
                // An IAM outage must surface — degrading it to an empty result hides the problem.
                LogUnauthorized(logger, ex.ProviderName, ex.Operation, ex);
                throw;
            }

            LogDegraded(logger, ex.ProviderName, ex.Operation, ex.Category, ex);
            return degraded;
        }
    }

    private string? CurrentTenantId() =>
        currentTenant is { IsAvailable: true } tenant ? tenant.Id.ToString() : null;

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Identity provider '{ProviderName}' operation '{Operation}' failed with an authorization error — surfacing to the caller.")]
    private static partial void LogUnauthorized(ILogger logger, string providerName, string operation, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Identity provider '{ProviderName}' operation '{Operation}' failed ({Category}) — degrading to an empty result.")]
    private static partial void LogDegraded(
        ILogger logger, string providerName, string operation, IdentityProviderFailureCategory category, Exception exception);
}
