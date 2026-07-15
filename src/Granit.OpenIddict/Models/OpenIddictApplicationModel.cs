
namespace Granit.OpenIddict.Models;

/// <summary>
/// Projection of an OpenIddict application for the query engine, exports, and entity metadata.
/// A framework-owned value shape — the EF entity (<c>GranitOpenIddictApplication</c>) lives in
/// <c>Granit.OpenIddict.EntityFrameworkCore</c> and is projected onto this model by the EF
/// <c>IQueryableSource</c> / <c>IExportDataSource</c>, so the base module carries no EF dependency.
/// </summary>
public sealed record OpenIddictApplicationModel
{
    /// <summary>The application identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>The owning tenant. <see langword="null"/> = global (visible to all tenants).</summary>
    public Guid? TenantId { get; init; }

    /// <summary>The client identifier (client_id).</summary>
    public string? ClientId { get; init; }

    /// <summary>The human-readable display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>The client type (public / confidential).</summary>
    public string? ClientType { get; init; }

    /// <summary>The consent type (explicit / implicit / external / systematic).</summary>
    public string? ConsentType { get; init; }

    /// <summary>The application type (web / native).</summary>
    public string? ApplicationType { get; init; }

    /// <summary>The JSON Web Key Set, serialized as JSON.</summary>
    public string? JsonWebKeySet { get; init; }

    /// <summary>Free-form properties, serialized as JSON.</summary>
    public string? Properties { get; init; }

    /// <summary>The granted permissions, serialized as a JSON array.</summary>
    public string? Permissions { get; init; }

    /// <summary>The allowed redirect URIs, serialized as a JSON array.</summary>
    public string? RedirectUris { get; init; }

    /// <summary>The allowed post-logout redirect URIs, serialized as a JSON array.</summary>
    public string? PostLogoutRedirectUris { get; init; }

    /// <summary>The requirements (e.g. PKCE), serialized as a JSON array.</summary>
    public string? Requirements { get; init; }
}
