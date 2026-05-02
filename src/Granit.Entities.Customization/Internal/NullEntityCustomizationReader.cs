using Granit.Entities.Customization.Domain;

namespace Granit.Entities.Customization.Internal;

/// <summary>
/// Default no-op reader registered by <c>AddGranitEntitiesCustomization</c>.
/// Hosts that omit the EF companion still boot — every lookup returns
/// <c>null</c> / empty so the manifest composer (B4) falls through to
/// compiled defaults.
/// </summary>
internal sealed class NullEntityCustomizationReader : IEntityCustomizationReader
{
    public Task<EntityCustomization?> GetAsync(
        string entityName,
        LayoutKind layoutKind,
        Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<EntityCustomization?>(null);

    public Task<IReadOnlyList<EntityCustomization>> GetForTenantAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<EntityCustomization>>([]);
}
