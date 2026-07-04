using Granit.Events;
using Granit.Settings.Definitions;
using Granit.Settings.Events;
using Granit.Settings.Providers;
using Granit.Settings.Values;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Settings.Services;

/// <summary>
/// Implementation of <see cref="ISettingWriter"/>: writes to <see cref="ISettingStoreWriter"/>,
/// invalidates the cache, and publishes <see cref="SettingChangedEvent"/> via <see cref="ILocalEventBus"/>.
/// </summary>
public sealed class SettingWriter(
    ISettingStoreWriter storeWriter,
    ISettingStoreReader storeReader,
    IFusionCache cache,
    SettingDefinitionRegistry definitions,
    ILocalEventBus eventBus,
    TimeProvider timeProvider) : ISettingWriter
{
    private readonly ISettingStoreWriter _storeWriter = storeWriter;
    private readonly ISettingStoreReader _storeReader = storeReader;
    private readonly IFusionCache _cache = cache;
    private readonly SettingDefinitionRegistry _definitions = definitions;
    private readonly ILocalEventBus _eventBus = eventBus;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <inheritdoc/>
    public async Task SetGlobalAsync(string name, string? value, CancellationToken cancellationToken = default)
    {
        SettingDefinition definition = _definitions.Get(name);
        const string providerName = GlobalSettingValueProvider.ProviderName;

        SettingValue? oldValue = await _storeReader
            .GetOrNullAsync(name, providerName, null, cancellationToken).ConfigureAwait(false);

        await _storeWriter.SetAsync(name, providerName, null, value, cancellationToken).ConfigureAwait(false);
        await _cache.ExpireAsync(
            SettingCacheKey.Build(providerName, null, name), token: cancellationToken).ConfigureAwait(false);

        await _eventBus.PublishAsync(
            new SettingChangedEvent(
                name, providerName, null,
                MaskIfEncrypted(definition, oldValue?.Value),
                MaskIfEncrypted(definition, value),
                _timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetForTenantAsync(Guid tenantId, string name, string? value, CancellationToken cancellationToken = default)
    {
        SettingDefinition definition = _definitions.Get(name);
        const string providerName = TenantSettingValueProvider.ProviderName;
        string tenantKey = tenantId.ToString();

        SettingValue? oldValue = await _storeReader
            .GetOrNullAsync(name, providerName, tenantKey, cancellationToken).ConfigureAwait(false);

        await _storeWriter.SetAsync(name, providerName, tenantKey, value, cancellationToken).ConfigureAwait(false);
        await _cache.ExpireAsync(
            SettingCacheKey.Build(providerName, tenantKey, name), token: cancellationToken).ConfigureAwait(false);

        await _eventBus.PublishAsync(
            new SettingChangedEvent(
                name, providerName, tenantKey,
                MaskIfEncrypted(definition, oldValue?.Value),
                MaskIfEncrypted(definition, value),
                _timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetForUserAsync(string userId, string name, string? value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        SettingDefinition definition = _definitions.Get(name);
        const string providerName = UserSettingValueProvider.ProviderName;

        SettingValue? oldValue = await _storeReader
            .GetOrNullAsync(name, providerName, userId, cancellationToken).ConfigureAwait(false);

        await _storeWriter.SetAsync(name, providerName, userId, value, cancellationToken).ConfigureAwait(false);
        await _cache.ExpireAsync(
            SettingCacheKey.Build(providerName, userId, name), token: cancellationToken).ConfigureAwait(false);

        await _eventBus.PublishAsync(
            new SettingChangedEvent(
                name, providerName, userId,
                MaskIfEncrypted(definition, oldValue?.Value),
                MaskIfEncrypted(definition, value),
                _timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string name,
        string providerName,
        string? providerKey = null,
        CancellationToken cancellationToken = default)
    {
        SettingDefinition definition = _definitions.Get(name);

        SettingValue? oldValue = await _storeReader
            .GetOrNullAsync(name, providerName, providerKey, cancellationToken).ConfigureAwait(false);

        await _storeWriter.DeleteAsync(name, providerName, providerKey, cancellationToken).ConfigureAwait(false);
        await _cache.ExpireAsync(
            SettingCacheKey.Build(providerName, providerKey, name), token: cancellationToken).ConfigureAwait(false);

        await _eventBus.PublishAsync(
            new SettingChangedEvent(
                name, providerName, providerKey,
                MaskIfEncrypted(definition, oldValue?.Value),
                null,
                _timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }

    private static string? MaskIfEncrypted(SettingDefinition definition, string? value) =>
        definition.IsEncrypted && value is not null ? "***" : value;
}
