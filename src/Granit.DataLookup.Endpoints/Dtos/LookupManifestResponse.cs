using Granit.DataLookup.Descriptors;

namespace Granit.DataLookup.Endpoints.Dtos;

/// <summary>Response shape for <c>GET /api/granit/lookups</c> — discovery manifest.</summary>
/// <param name="Lookups">Sorted list of registered lookups.</param>
public sealed record LookupManifestResponse(IReadOnlyList<LookupManifestEntryResponse> Lookups);

/// <summary>A single entry in the lookup manifest.</summary>
/// <param name="Name">Registry key.</param>
/// <param name="Kind">Kind of backing source.</param>
/// <param name="RequiredPermission">Permission required to invoke the source, if any.</param>
/// <param name="ScopeKeys">Scope keys the source requires with every query.</param>
public sealed record LookupManifestEntryResponse(
    string Name,
    LookupKind Kind,
    string? RequiredPermission,
    IReadOnlyList<string> ScopeKeys);
