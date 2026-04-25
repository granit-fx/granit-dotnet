using Granit.DataLookup.Descriptors;
using Granit.DataLookup.Sources;

namespace Granit.DataLookup.Registry;

/// <summary>
/// Default scoped <see cref="ILookupRegistry"/> implementation. Aggregates every
/// <see cref="ILookupSource"/> registered in DI and exposes them by <see cref="ILookupSource.Name"/>.
/// </summary>
/// <remarks>
/// Registered as scoped so that sources with scoped dependencies (e.g. a
/// QueryDefinitionLookupSource that needs an <c>IQueryEngine&lt;T&gt;</c>) resolve
/// correctly. The per-request cost is a single dictionary build over the enumerable
/// — negligible compared to the lookup's own database roundtrip.
/// </remarks>
internal sealed class LookupRegistry : ILookupRegistry
{
    private readonly Dictionary<string, ILookupSource> _sources;

    public LookupRegistry(IEnumerable<ILookupSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        _sources = new(StringComparer.Ordinal);
        foreach (ILookupSource source in sources)
        {
            if (!_sources.TryAdd(source.Name, source))
            {
                throw new InvalidOperationException(
                    $"Duplicate lookup source name '{source.Name}'. Every ILookupSource must have a globally unique Name.");
            }
        }
    }

    public ILookupSource? Resolve(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _sources.TryGetValue(name, out ILookupSource? source) ? source : null;
    }

    public IReadOnlyList<LookupManifestEntry> GetManifest() =>
        [.. _sources.Values
            .Select(source => new LookupManifestEntry(
                source.Name,
                InferKind(source),
                source.RequiredPermission,
                source.ScopeKeys))
            .OrderBy(entry => entry.Name, StringComparer.Ordinal)];

    private static LookupKind InferKind(ILookupSource source) =>
        source is IKindProviderLookupSource provider ? provider.Kind : LookupKind.Simple;
}
