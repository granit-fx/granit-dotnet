using Granit.Entities.Customization.Domain;

namespace Granit.Entities.Customization;

/// <summary>
/// Read side of the customization repository (CQRS split — see CLAUDE.md
/// anti-pattern §"Merge I*Reader/I*Writer into I*Store"). Implemented by
/// <c>Granit.Entities.Customization.EntityFrameworkCore</c> at runtime;
/// hosts that omit the EF companion get the no-op
/// <see cref="Internal.NullEntityCustomizationReader"/> by default so the
/// manifest composer (B4) still resolves to compiled defaults.
/// </summary>
public interface IEntityCustomizationReader
{
    /// <summary>
    /// Resolves the (tenant, entity, layout) customization, or <c>null</c>
    /// when the tenant has not customized that layout.
    /// </summary>
    Task<EntityCustomization?> GetAsync(
        string entityName,
        LayoutKind layoutKind,
        Guid? tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every customization owned by <paramref name="tenantId"/>.
    /// Used by the admin UI to show the tenant the full set of overrides.
    /// </summary>
    Task<IReadOnlyList<EntityCustomization>> GetForTenantAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default);
}
