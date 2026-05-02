namespace Granit.Activities.Internal;

/// <summary>
/// Default <see cref="IActivityRegistry"/> built from the DI-injected
/// enumerable of <see cref="IActivityTypeProvider"/>. Aggregates every
/// provider's contributions at construction; fails fast on duplicate
/// <see cref="ActivityType.Name"/>.
/// </summary>
internal sealed class ActivityRegistry : IActivityRegistry
{
    private readonly Dictionary<string, ActivityType> _byName;

    public ActivityRegistry(IEnumerable<IActivityTypeProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        _byName = new Dictionary<string, ActivityType>(StringComparer.Ordinal);

        foreach (IActivityTypeProvider provider in providers)
        {
            foreach (ActivityType type in provider.Provide())
            {
                ArgumentNullException.ThrowIfNull(type);
                ArgumentException.ThrowIfNullOrWhiteSpace(type.Name);

                if (!_byName.TryAdd(type.Name, type))
                {
                    throw new InvalidOperationException(
                        $"Activity type '{type.Name}' is contributed by more than one IActivityTypeProvider. Names must be unique across all loaded providers.");
                }
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, ActivityType> All => _byName;

    /// <inheritdoc/>
    public bool TryGet(string name, out ActivityType type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _byName.TryGetValue(name, out type!);
    }
}
