using Granit.Settings.Definitions;
using Granit.Settings.Options;
using Granit.Settings.Values;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Settings.Providers;

/// <summary>
/// Global settings provider (application scope, no tenant/user isolation).
/// Caches values read from <see cref="ISettingStoreReader"/> (order = 300).
/// </summary>
public sealed class GlobalSettingValueProvider(
    ISettingStoreReader storeReader,
    ISettingStoreWriter storeWriter,
    IFusionCache cache,
    IOptions<SettingsOptions> options) : ISettingValueProvider
{
    /// <summary>Global provider identifier.</summary>
    public const string ProviderName = "G";

    private readonly ISettingStoreReader _storeReader = storeReader;
    private readonly ISettingStoreWriter _storeWriter = storeWriter;
    private readonly IFusionCache _cache = cache;
    private readonly IOptions<SettingsOptions> _options = options;

    /// <inheritdoc/>
    public string Name => ProviderName;

    /// <inheritdoc/>
    public int Order => 300;

    /// <inheritdoc/>
    public async Task<SettingValue?> GetOrNullAsync(SettingDefinition definition, CancellationToken cancellationToken = default)
    {
        string cacheKey = SettingCacheKey.Build(ProviderName, null, definition.Name);
        var cacheOptions = new FusionCacheEntryOptions { Duration = _options.Value.CacheExpiration };

        return await _cache.GetOrSetAsync<SettingValue>(
            cacheKey,
            async (_, ct) =>
            {
                SettingValue? stored = await _storeReader.GetOrNullAsync(
                    definition.Name, ProviderName, null, ct).ConfigureAwait(false);
                // Sentinel to distinguish "stored null" from "absent from cache"
                return stored ?? new SettingValue(definition.Name, ProviderName, null, null);
            },
            cacheOptions,
            token: cancellationToken).ConfigureAwait(false) is { Value: not null } hit
            ? hit
            : null;
    }

    /// <inheritdoc/>
    public async Task SetAsync(SettingDefinition definition, string? value, CancellationToken cancellationToken = default)
    {
        await _storeWriter.SetAsync(definition.Name, ProviderName, null, value, cancellationToken).ConfigureAwait(false);
        await _cache.ExpireAsync(SettingCacheKey.Build(ProviderName, null, definition.Name), token: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ClearAsync(SettingDefinition definition, CancellationToken cancellationToken = default)
    {
        await _storeWriter.DeleteAsync(definition.Name, ProviderName, null, cancellationToken).ConfigureAwait(false);
        await _cache.ExpireAsync(SettingCacheKey.Build(ProviderName, null, definition.Name), token: cancellationToken).ConfigureAwait(false);
    }
}
