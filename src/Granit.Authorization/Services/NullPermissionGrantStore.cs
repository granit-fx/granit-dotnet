using Microsoft.Extensions.Logging;

namespace Granit.Authorization.Services;

/// <summary>
/// Default no-op implementation of <see cref="IPermissionGrantStore"/>.
/// Always returns false / empty — all permissions are denied unless overridden by AdminRole bypass
/// or <see cref="Options.GranitAuthorizationOptions.AlwaysAllow"/>.
/// Write operations are no-ops and log a warning.
/// Replace with <c>Granit.Authorization.EntityFrameworkCore</c> for persistence.
/// </summary>
internal sealed partial class NullPermissionGrantStore(
    ILogger<NullPermissionGrantStore> logger) : IPermissionGrantStore
{
    private static readonly IReadOnlyList<string> Empty = [];

    /// <inheritdoc />
    public Task<bool> IsGrantedAsync(
        string providerName,
        string providerKey,
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default) => Task.FromResult(false);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetGrantedPermissionsAsync(
        string providerName,
        string providerKey,
        Guid? tenantId,
        CancellationToken cancellationToken = default) => Task.FromResult(Empty);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetGrantedAsync(
        string providerName,
        string providerKey,
        IReadOnlyList<string> permissionNames,
        Guid? tenantId,
        CancellationToken cancellationToken = default) => Task.FromResult(Empty);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetGranteesAsync(
        string providerName,
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default) => Task.FromResult(Empty);

    /// <inheritdoc />
    public Task<bool> GrantAsync(
        string providerName,
        string providerKey,
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        LogWriteDiscarded("Grant", permissionName, providerName, providerKey);
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<bool> RevokeAsync(
        string providerName,
        string providerKey,
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        LogWriteDiscarded("Revoke", permissionName, providerName, providerKey);
        return Task.FromResult(false);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "NullPermissionGrantStore is active — {Operation} for permission={PermissionName} provider={ProviderName} key={ProviderKey} was discarded. " +
                  "Register Granit.Authorization.EntityFrameworkCore for persistence.")]
    private partial void LogWriteDiscarded(string operation, string permissionName, string providerName, string providerKey);
}
