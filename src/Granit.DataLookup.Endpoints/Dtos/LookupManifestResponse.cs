namespace Granit.DataLookup.Endpoints.Dtos;

/// <summary>Response shape for <c>GET /api/granit/lookups</c> — discovery manifest.</summary>
/// <param name="Lookups">Sorted list of registered lookups.</param>
public sealed record LookupManifestResponse(IReadOnlyList<LookupManifestEntryResponse> Lookups);
