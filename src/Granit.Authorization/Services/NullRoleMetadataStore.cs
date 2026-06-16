using Granit.Authorization.Domain;

namespace Granit.Authorization.Services;

/// <summary>
/// No-op <see cref="IRoleMetadataStore"/> registered when no EF Core implementation
/// is wired. Every read returns no role and every write throws.
/// </summary>
/// <remarks>
/// Useful for consumers that reference <c>Granit.Authorization</c> but never persist
/// role metadata (tests, read-only projections, providers that rely solely on grants).
/// </remarks>
internal sealed class NullRoleMetadataStore : IRoleMetadataStore
{
    /// <inheritdoc />
    public Task<RoleMetadata?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<RoleMetadata?>(null);

    /// <inheritdoc />
    public Task<RoleMetadata?> FindByNameAsync(
        string name, Guid? tenantId, string? clientId, CancellationToken cancellationToken = default) =>
        Task.FromResult<RoleMetadata?>(null);

    /// <inheritdoc />
    public Task<IReadOnlyList<RoleMetadata>> ListAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RoleMetadata>>([]);

    /// <inheritdoc />
    public Task<IReadOnlyList<RoleMetadata>> ListByClientIdAsync(
        string? clientId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RoleMetadata>>([]);

    /// <inheritdoc />
    public Task AddAsync(RoleMetadata role, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "No IRoleMetadataStore implementation is registered. " +
            "Add Granit.Authorization.EntityFrameworkCore (or an equivalent provider) and wire the store.");

    /// <inheritdoc />
    public Task UpdateAsync(RoleMetadata role, string? concurrencyStamp = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "No IRoleMetadataStore implementation is registered.");

    /// <inheritdoc />
    public Task RemoveAsync(RoleMetadata role, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "No IRoleMetadataStore implementation is registered.");
}
