using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Querying.SavedViews.Domain;

/// <summary>
/// Persistent saved view combining filters, sorting, grouping, and column visibility
/// (inspired by Odoo's <c>ir.filters</c> model).
/// </summary>
public sealed class SavedView : AuditedEntity, IMultiTenant
{
    /// <summary>
    /// The entity type this view applies to (e.g. <c>"Acme.Patients"</c>).
    /// Maps to <see cref="IQueryDefinitionDescriptor.Name"/>.
    /// </summary>
    public required string EntityType { get; set; }

    /// <summary>
    /// User-facing name of the saved view.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Identifier of the user who created this view.
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    /// Whether this view is shared with other users (vs. personal).
    /// </summary>
    public bool IsShared { get; set; }

    /// <summary>
    /// Whether this is the user's default view for the entity type.
    /// At most one default per user per entity type.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Serialized filter criteria (JSON), or <c>null</c>.
    /// </summary>
    public string? FilterJson { get; set; }

    /// <summary>
    /// Serialized sort specification (JSON), or <c>null</c>.
    /// </summary>
    public string? SortJson { get; set; }

    /// <summary>
    /// Serialized group-by specification (JSON), or <c>null</c>.
    /// </summary>
    public string? GroupByJson { get; set; }

    /// <summary>
    /// Serialized visible column list (JSON), or <c>null</c>.
    /// </summary>
    public string? VisibleColumnsJson { get; set; }

    /// <summary>
    /// Tenant identifier for multi-tenant isolation, or <c>null</c>.
    /// </summary>
    public Guid? TenantId { get; set; }
}
