using Granit.DataLookup.Descriptors;

namespace Granit.DataLookup.Registry;

/// <summary>
/// Public metadata about a registered lookup source — one entry per registered
/// <see cref="Sources.ILookupSource"/> in the <c>GET /lookups</c> manifest.
/// </summary>
/// <param name="Name">Registry key.</param>
/// <param name="Kind">Kind of backing source.</param>
/// <param name="RequiredPermission">Permission required to invoke the source, if any.</param>
/// <param name="ScopeKeys">Scope keys this source requires.</param>
public sealed record LookupManifestEntry(
    string Name,
    LookupKind Kind,
    string? RequiredPermission,
    IReadOnlyList<string> ScopeKeys);
