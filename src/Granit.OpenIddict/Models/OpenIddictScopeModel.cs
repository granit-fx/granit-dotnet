
namespace Granit.OpenIddict.Models;

/// <summary>
/// Projection of an OpenIddict scope for the query engine, exports, and entity metadata.
/// A framework-owned value shape — the EF entity (<c>GranitOpenIddictScope</c>) lives in
/// <c>Granit.OpenIddict.EntityFrameworkCore</c> and is projected onto this model by the EF
/// <c>IQueryableSource</c> / <c>IExportDataSource</c>, so the base module carries no EF dependency.
/// </summary>
public sealed record OpenIddictScopeModel
{
    /// <summary>The scope identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>The owning tenant. <see langword="null"/> = global (visible to all tenants).</summary>
    public Guid? TenantId { get; init; }

    /// <summary>The scope name.</summary>
    public string? Name { get; init; }

    /// <summary>The human-readable display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The description.</summary>
    public string? Description { get; init; }

    /// <summary>Free-form properties, serialized as JSON.</summary>
    public string? Properties { get; init; }

    /// <summary>The resources the scope grants access to, serialized as a JSON array.</summary>
    public string? Resources { get; init; }
}
