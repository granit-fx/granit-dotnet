using Granit.AI.Permissions;
using Granit.Authorization;
using Granit.Settings.Services;

namespace Granit.AI.Tenancy;

/// <summary>
/// Decorator on <see cref="ISettingManager"/> that rejects writes to any setting whose name
/// starts with <see cref="AISettingNames.PrefixValue"/> unless the caller has the
/// <see cref="AIPermissions.Credentials.Manage"/> permission.
/// </summary>
/// <remarks>
/// <para>
/// Closes the permission-boundary gap identified in audit VULN-100: AI credentials were
/// previously gated only by <c>Settings.{Tenant,Global}.Manage</c>, which is too broad —
/// a routine config admin could redirect AI traffic to an attacker endpoint or rotate
/// the credential under which prompts are sent.
/// </para>
/// <para>
/// Registered as a decorator over the underlying <see cref="ISettingManager"/> in DI.
/// Reads are not gated by this layer: the read endpoints already check
/// <c>Settings.*.Read</c>, and <c>IsVisibleToClients=false</c> on AI definitions ensures
/// the public-facing user settings API doesn't return them.
/// </para>
/// </remarks>
internal sealed class AISettingsCredentialsGuard(
    ISettingManager inner,
    IPermissionChecker permissionChecker) : ISettingManager
{
    /// <inheritdoc />
    public async Task SetGlobalAsync(string name, string? value, CancellationToken cancellationToken = default)
    {
        await EnsureAuthorisedAsync(name, cancellationToken).ConfigureAwait(false);
        await inner.SetGlobalAsync(name, value, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetForTenantAsync(Guid tenantId, string name, string? value, CancellationToken cancellationToken = default)
    {
        await EnsureAuthorisedAsync(name, cancellationToken).ConfigureAwait(false);
        await inner.SetForTenantAsync(tenantId, name, value, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetForUserAsync(string userId, string name, string? value, CancellationToken cancellationToken = default)
    {
        // AI credentials are never user-scoped (Providers = { T, G }), so a user write to
        // a Granit.AI.* setting would fail at the store layer. We still guard here for symmetry —
        // never let a future change open this door silently.
        await EnsureAuthorisedAsync(name, cancellationToken).ConfigureAwait(false);
        await inner.SetForUserAsync(userId, name, value, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        string name,
        string providerName,
        string? providerKey = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthorisedAsync(name, cancellationToken).ConfigureAwait(false);
        await inner.DeleteAsync(name, providerName, providerKey, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EnsureAuthorisedAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (!name.StartsWith(AISettingNames.PrefixValue, StringComparison.Ordinal))
        {
            return;
        }

        bool granted = await permissionChecker
            .IsGrantedAsync(AIPermissions.Credentials.Manage, cancellationToken)
            .ConfigureAwait(false);

        if (!granted)
        {
            throw new UnauthorizedAccessException(
                $"Writing AI setting '{name}' requires the '{AIPermissions.Credentials.Manage}' permission.");
        }
    }
}
