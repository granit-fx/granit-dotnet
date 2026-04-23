using Granit.DataLookup.Descriptors;

namespace Granit.DataLookup.Sources;

/// <summary>
/// A source of values that a user can pick from in a filter, a dropdown, or any other
/// selection UI. Every registered source is discoverable by its unique <see cref="Name"/>
/// and exposes a uniform search / resolve API that returns the canonical
/// <see cref="LookupItem"/> shape.
/// </summary>
/// <remarks>
/// <para>
/// Implementations MUST:
/// </para>
/// <list type="bullet">
///   <item><description>
///   Resolve labels in the caller's culture (<see cref="System.Globalization.CultureInfo.CurrentUICulture"/>)
///   before projecting to <see cref="LookupItem.Label"/>.
///   </description></item>
///   <item><description>
///   Validate that every scope key declared in <see cref="ScopeKeys"/> is present and
///   non-empty in <see cref="LookupQuery.Scope"/>. Missing keys are a client error
///   (400), not a fallback to an unscoped query.
///   </description></item>
///   <item><description>
///   Respect the ambient <c>ICurrentTenant</c> for multi-tenant isolation. Authorization
///   (<see cref="RequiredPermission"/>) is enforced by the endpoint layer before the
///   source is invoked.
///   </description></item>
/// </list>
/// </remarks>
public interface ILookupSource
{
    /// <summary>
    /// Unique registry key (e.g. <c>"tenants"</c>, <c>"ref-countries"</c>,
    /// <c>"enum-aggregation-type"</c>).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Permission the caller must hold. <see langword="null"/> means the lookup is
    /// public within the caller's tenant.
    /// </summary>
    string? RequiredPermission { get; }

    /// <summary>
    /// Names of scope keys this source requires (e.g. <c>["tenantId"]</c>). Missing
    /// keys at query time result in a 400 response.
    /// </summary>
    IReadOnlyList<string> ScopeKeys { get; }

    /// <summary>Searches for matching items, optionally filtered by <paramref name="query"/>.</summary>
    ValueTask<LookupResult> SearchAsync(LookupQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a single item by its value. Used by the frontend to rehydrate a
    /// previously selected identifier into a human-readable label.
    /// </summary>
    /// <returns>The item if found, otherwise <see langword="null"/>.</returns>
    ValueTask<LookupItem?> ResolveByValueAsync(object value, CancellationToken cancellationToken);
}
