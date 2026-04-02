using Granit.Authorization.Abstractions;

namespace Granit.Authorization.Services;

/// <summary>
/// Default no-op implementation of <see cref="IPermissionGrantStore"/>.
/// Always returns false / empty — all permissions are denied unless overridden by AdminRole bypass
/// or <see cref="Options.GranitAuthorizationOptions.AlwaysAllow"/>.
/// Write operations are no-ops.
/// Replace with <c>Granit.Authorization.EntityFrameworkCore</c> for persistence.
/// </summary>
internal sealed class NullPermissionGrantStore : IPermissionGrantStore
{
    private static readonly IReadOnlyList<string> Empty = [];

    /// <inheritdoc />
    public Task<bool> IsGrantedAsync(
        string roleName,
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default) => Task.FromResult(false);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetGrantedPermissionsAsync(
        string roleName,
        Guid? tenantId,
        CancellationToken cancellationToken = default) => Task.FromResult(Empty);

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> GetGrantedRolesAsync(
        string permissionName,
        Guid? tenantId,
        CancellationToken cancellationToken = default) => Task.FromResult(Empty);

    /// <inheritdoc />
    public Task<bool> GrantAsync(
        string permissionName,
        string roleName,
        Guid? tenantId,
        CancellationToken cancellationToken = default) => Task.FromResult(false);

    /// <inheritdoc />
    public Task<bool> RevokeAsync(
        string permissionName,
        string roleName,
        Guid? tenantId,
        CancellationToken cancellationToken = default) => Task.FromResult(false);
}
