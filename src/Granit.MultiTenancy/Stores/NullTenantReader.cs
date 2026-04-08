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

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
