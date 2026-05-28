using Granit.MultiTenancy.Stores;

namespace Granit.MultiTenancy.Internal;

/// <summary>
/// Adapts the rich <see cref="ITenantReader"/> aggregate query to the lightweight
/// <see cref="ITenantsAccessor"/> primitive exposed by base <c>Granit</c>.
/// </summary>
/// <remarks>
/// Registered by <c>AddGranitMultiTenancy</c> as a <c>Replace</c> on the default
/// <c>NullTenantsAccessor</c>. Soft-dep modules (Webhooks, Identity.Federated,
/// future Notifications/Auditing under Segregated host-admin paths) inject
/// <see cref="ITenantsAccessor"/> directly and remain free of any
/// <c>Granit.MultiTenancy</c> package reference.
/// </remarks>
internal sealed class TenantReaderTenantsAccessorAdapter(ITenantReader reader) : ITenantsAccessor
{
    public async Task<IReadOnlyList<(Guid Id, string Name)>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TenantData> tenants = await reader
            .GetAllAsync(cancellationToken).ConfigureAwait(false);

        return [.. tenants.Select(t => (t.Id, t.Name))];
    }
}
