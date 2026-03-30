using System.Collections.Concurrent;

namespace Granit.Privacy.DataExport.Internal;

/// <summary>
/// Thread-safe singleton registry of data providers participating in personal data export/deletion.
/// </summary>
internal sealed class DataProviderRegistry : IDataProviderRegistry
{
    private readonly ConcurrentDictionary<string, byte> _providers = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public void Register(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        if (!_providers.TryAdd(providerName, 0))
        {
            throw new InvalidOperationException($"Data provider '{providerName}' is already registered.");
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAll() =>
        _providers.Keys.ToList();

    /// <inheritdoc/>
    public int Count => _providers.Count;
}
