using Granit.Domain;

namespace Granit.DataExchange.Export;

/// <summary>
/// Convenience extensions for declaring the standard audit fields on an
/// <see cref="ExportDefinitionBuilder{TEntity}"/> without repeating them in every definition.
/// </summary>
public static class ExportDefinitionBuilderAuditExtensions
{
    /// <summary>
    /// Appends the creation audit fields — <see cref="ICreationAuditedObject.CreatedAt"/> (round-trip
    /// <c>"O"</c> format) and <see cref="ICreationAuditedObject.CreatedBy"/>.
    /// </summary>
    public static ExportDefinitionBuilder<TEntity> IncludeCreationAuditFields<TEntity>(
        this ExportDefinitionBuilder<TEntity> builder)
        where TEntity : class, ICreationAuditedObject
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .Field(e => e.CreatedAt, f => f.Format("O"))
            .Field(e => e.CreatedBy);
    }

    /// <summary>
    /// Appends the full creation + modification audit fields — <c>CreatedAt</c>, <c>CreatedBy</c>,
    /// <c>ModifiedAt</c> (round-trip <c>"O"</c> format) and <c>ModifiedBy</c> — the shape shared by every
    /// <see cref="IModificationAuditedObject"/>. Replaces the four field declarations repeated across
    /// export definitions.
    /// </summary>
    public static ExportDefinitionBuilder<TEntity> IncludeAuditFields<TEntity>(
        this ExportDefinitionBuilder<TEntity> builder)
        where TEntity : class, ICreationAuditedObject, IModificationAuditedObject
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .Field(e => e.CreatedAt, f => f.Format("O"))
            .Field(e => e.CreatedBy)
            .Field(e => e.ModifiedAt, f => f.Format("O"))
            .Field(e => e.ModifiedBy);
    }
}
