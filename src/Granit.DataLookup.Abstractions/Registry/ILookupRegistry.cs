using Granit.DataLookup.Sources;

namespace Granit.DataLookup.Registry;

/// <summary>
/// Central registry that maps registered lookup names to their
/// <see cref="ILookupSource"/> implementation.
/// </summary>
/// <remarks>
/// Registered at startup via <c>AddLookup*</c> extensions on <c>IServiceCollection</c>.
/// Resolved at runtime by the lookup endpoints to dispatch
/// <c>GET /api/granit/lookups/{name}</c> to the correct source.
/// </remarks>
public interface ILookupRegistry
{
    /// <summary>Returns the source registered under <paramref name="name"/>, or <see langword="null"/>.</summary>
    ILookupSource? Resolve(string name);

    /// <summary>
    /// Returns a manifest of every registered source for
    /// <c>GET /api/granit/lookups</c> (for tooling and frontend discovery).
    /// </summary>
    IReadOnlyList<LookupManifestEntry> GetManifest();
}
