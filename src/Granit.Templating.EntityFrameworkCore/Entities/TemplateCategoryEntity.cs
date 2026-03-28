using Granit.Domain;

namespace Granit.Templating.EntityFrameworkCore.Entities;

/// <summary>
/// EF Core entity for template categories used to organize templates by domain.
/// Implements <see cref="IMultiTenant"/> for per-tenant category isolation.
/// </summary>
internal sealed class TemplateCategoryEntity : IMultiTenant
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Display name (e.g. "Patient letters"). Must be unique.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Optional description.</summary>
    public string? Description { get; set; }

    /// <summary>Optional Lucide icon name (e.g. "file-text").</summary>
    public string? Icon { get; set; }

    /// <summary>Display order (ascending).</summary>
    public int SortOrder { get; set; }

    /// <summary>UTC timestamp when this category was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Identity of the user who created this category.</summary>
    public string CreatedBy { get; set; } = null!;

    /// <inheritdoc/>
    public Guid? TenantId { get; set; }
}
