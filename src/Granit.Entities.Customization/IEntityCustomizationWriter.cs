using Granit.Entities.Customization.Domain;

namespace Granit.Entities.Customization;

/// <summary>
/// Write side of the customization repository. The <c>PUT</c> endpoint (B3)
/// upserts a single customization row; revert-to-default deletes it. No host
/// uses these contracts directly — they are consumed by the endpoint layer
/// only.
/// </summary>
public interface IEntityCustomizationWriter
{
    /// <summary>
    /// Inserts or replaces the customization. The (tenant, entity, layout)
    /// triple is treated as the natural key; the EF companion (B2) carries
    /// the matching unique constraint.
    /// </summary>
    Task UpsertAsync(EntityCustomization customization, CancellationToken cancellationToken = default);

    /// <summary>Deletes the customization by id. Used to revert to compiled defaults.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
