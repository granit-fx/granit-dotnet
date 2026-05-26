using System.Collections.Concurrent;

namespace Granit.Privacy.DataExport.Internal;

/// <summary>
/// Thread-safe singleton registry of data providers participating in personal data export/deletion.
/// </summary>
internal sealed class DataProviderRegistry : IDataProviderRegistry
{
    private readonly ConcurrentDictionary<string, ProviderRegistration> _providers =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public void Register(string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        ProviderRegistration legacyRegistration = new(
            ProviderName: providerName,
            DisplayKey: providerName,
            FeatureName: null,
            HasDataProbe: static (_, _, _) => ValueTask.FromResult(true));

        if (!_providers.TryAdd(providerName, legacyRegistration))
        {
            throw new InvalidOperationException($"Data provider '{providerName}' is already registered.");
        }
    }

    /// <inheritdoc/>
    public void Register(ProviderRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentException.ThrowIfNullOrWhiteSpace(registration.ProviderName);

        if (!_providers.TryAdd(registration.ProviderName, registration))
        {
            throw new InvalidOperationException($"Data provider '{registration.ProviderName}' is already registered.");
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAll() => [.. _providers.Keys];

    /// <inheritdoc/>
    public IReadOnlyList<ProviderRegistration> GetAllRegistrations() => [.. _providers.Values];

    /// <inheritdoc/>
    public int Count => _providers.Count;
}
