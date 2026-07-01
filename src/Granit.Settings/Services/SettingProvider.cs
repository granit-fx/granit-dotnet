using Granit.Settings.Definitions;
using Granit.Settings.Providers;
using Granit.Settings.Values;

namespace Granit.Settings.Services;

/// <summary>
/// Implementation of <see cref="ISettingProvider"/> with cascading resolution.
/// </summary>
public sealed class SettingProvider(
    SettingValueProviderRegistry providerManager,
    SettingDefinitionRegistry definitionManager) : ISettingProvider
{
    private readonly SettingValueProviderRegistry _providerManager = providerManager;
    private readonly SettingDefinitionRegistry _definitionManager = definitionManager;

    /// <inheritdoc/>
    public async Task<string?> GetOrNullAsync(string name, CancellationToken cancellationToken = default)
    {
        SettingValue? resolved = await ResolveAsync(name, cancellationToken).ConfigureAwait(false);
        return resolved?.Value;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SettingValue>> GetAllAsync(string[] names, CancellationToken cancellationToken = default)
    {
        List<SettingValue> result = new(names.Length);
        foreach (string name in names)
        {
            SettingValue? resolved = await ResolveAsync(name, cancellationToken).ConfigureAwait(false);
            result.Add(resolved ?? new SettingValue(name, string.Empty, null, null));
        }
        return result;
    }

    private async Task<SettingValue?> ResolveAsync(string name, CancellationToken cancellationToken)
    {
        SettingDefinition? definition = _definitionManager.GetOrNull(name);
        if (definition is null)
        {
            return null;
        }

        bool hasAllowList = definition.Providers.Count > 0;

        foreach (ISettingValueProvider provider in _providerManager.Providers)
        {
            if (hasAllowList && !definition.Providers.Contains(provider.Name))
            {
                continue;
            }

            SettingValue? value = await provider.GetOrNullAsync(definition, cancellationToken).ConfigureAwait(false);
            if (value is not null)
            {
                return value;
            }

            // Without inheritance, do not fall back to lower-priority levels
            if (!definition.IsInherited)
            {
                return null;
            }
        }

        return null;
    }
}
