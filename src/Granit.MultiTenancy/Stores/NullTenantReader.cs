namespace Granit.MultiTenancy.Stores;

/// <summary>
/// No-op <see cref="ITenantReader"/> used when no EF Core persistence is registered.
/// Replaced by <c>EfCoreTenantStore</c> when <c>Granit.MultiTenancy.EntityFrameworkCore</c>
/// is added to the module tree.
/// </summary>
internal sealed class NullTenantReader : ITenantReader
{
    public Task<TenantData?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<TenantData?>(null);

    public Task<TenantData?> FindByIdentifierAsync(string identifier, CancellationToken cancellationToken = default) =>
        Task.FromResult<TenantData?>(null);

    public Task<IReadOnlyList<TenantData>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TenantData>>([]);

    /// <summary>
    /// Returns <c>true</c> — when no real tenant store is registered, validation
    /// is skipped (all tenant IDs are accepted). The real <c>EfCoreTenantStore</c>
    /// performs the actual lookup.
    /// </summary>
    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
