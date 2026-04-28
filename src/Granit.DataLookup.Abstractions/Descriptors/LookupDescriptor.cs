namespace Granit.DataLookup.Descriptors;

/// <summary>
/// Declarative pointer to a lookup data source, emitted alongside column / field metadata
/// (e.g., <c>FilterableField.Lookup</c>) or referenced directly from an edit-form field.
/// </summary>
/// <remarks>
/// <para>
/// Exactly one of <see cref="Name"/> or <see cref="Endpoint"/> MUST be supplied:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///     <see cref="Name"/> — resolved against the central <see cref="Registry.ILookupRegistry"/>
///     (recommended). The frontend issues
///     <c>GET /lookups/{Name}?search=&amp;scope.{key}=…</c>.
///     </description>
///   </item>
///   <item>
///     <description>
///     <see cref="Endpoint"/> — fallback to a custom HTTP endpoint that returns the
///     canonical <see cref="LookupResult"/> shape. Used when a lookup points to an
///     external system or a bespoke route that isn't registry-managed.
///     </description>
///   </item>
/// </list>
/// </remarks>
/// <param name="Name">
/// Registry key of the lookup (e.g. <c>"tenants"</c>). Must be unique across all registered
/// sources. Omit if <paramref name="Endpoint"/> is supplied.
/// </param>
/// <param name="Endpoint">
/// Absolute or relative URL that returns a <see cref="LookupResult"/>. Omit if
/// <paramref name="Name"/> is supplied.
/// </param>
/// <param name="Kind">
/// Kind of backing source. Informational only — does not affect routing.
/// </param>
/// <param name="RequiredPermission">
/// Permission string (e.g. <c>"MultiTenancy.Tenants.Read"</c>) the principal must hold to
/// call the lookup. When null, the lookup is considered public (within tenant scope).
/// The frontend also uses this to hide the picker from users who lack the permission.
/// </param>
/// <param name="SearchParam">
/// Query-string parameter name to forward the user's typeahead text. Defaults to
/// <c>"search"</c>. Only honored when <see cref="Kind"/> is <see cref="LookupKind.Simple"/>.
/// QueryEngine endpoints always use <c>search</c>.
/// </param>
/// <param name="ScopeKeys">
/// Names of scope parameters the source requires (e.g. <c>["tenantId"]</c>). The frontend
/// is responsible for resolving the matching values from the surrounding form or filter
/// context and passing them as <c>scope.{key}=&lt;value&gt;</c>. When any declared scope
/// key is missing or empty, the frontend MUST NOT fire the lookup request and the backend
/// responds <c>400 Bad Request</c> — no silent full-table scan.
/// </param>
public sealed record LookupDescriptor(
    string? Name = null,
    string? Endpoint = null,
    LookupKind Kind = LookupKind.QueryEngine,
    string? RequiredPermission = null,
    string? SearchParam = "search",
    IReadOnlyList<string>? ScopeKeys = null);
