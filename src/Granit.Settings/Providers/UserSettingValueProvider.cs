using Granit.Security;
using Granit.Settings.Definitions;
using Granit.Settings.Options;
using Granit.Settings.Values;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Settings.Providers;

/// <summary>
/// User settings provider (isolated per current user via <see cref="ICurrentUserService"/>).
/// Caches values read from <see cref="ISettingStoreReader"/> (order = 100).
/// </summary>
public sealed class UserSettingValueProvider(
    ICurrentUserService currentUser,
    ISettingStoreReader storeReader,
    ISettingStoreWriter storeWriter,
    IFusionCache cache,
    IOptions<SettingsOptions> options) : ISettingValueProvider
{
    /// <summary>User provider identifier.</summary>
    public const string ProviderName = "U";

    private readonly ICurrentUserService _currentUser = currentUser;
    private readonly ISettingStoreReader _storeReader = storeReader;
    private readonly ISettingStoreWriter _storeWriter = storeWriter;
    private readonly IFusionCache _cache = cache;
    private readonly IOptions<SettingsOptions> _options = options;

    /// <inheritdoc/>
    public string Name => ProviderName;

    /// <inheritdoc/>
    public int Order => 100;

    /// <inheritdoc/>
    public async Task<SettingValue?> GetOrNullAsync(SettingDefinition definition, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            return null;
        }

        string userId = _currentUser.UserId;
        string cacheKey = SettingCacheKey.Build(ProviderName, userId, definition.Name);
        var cacheOptions = new FusionCacheEntryOptions { Duration = _options.Value.CacheExpiration };

        return await _cache.GetOrSetAsync<SettingValue>(
            cacheKey,
            async (_, ct) =>
            {
                SettingValue? stored = await _storeReader.GetOrNullAsync(
                    definition.Name, ProviderName, userId, ct).ConfigureAwait(false);
                return stored ?? new SettingValue(definition.Name, ProviderName, userId, null);
            },
            cacheOptions,
            token: cancellationToken).ConfigureAwait(false) is { Value: not null } hit
            ? hit
            : null;
    }

    /// <inheritdoc/>
    public async Task SetAsync(SettingDefinition definition, string? value, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            return;
        }

        string userId = _currentUser.UserId;
        await _storeWriter.SetAsync(definition.Name, ProviderName, userId, value, cancellationToken).ConfigureAwait(false);
        await _cache.ExpireAsync(SettingCacheKey.Build(ProviderName, userId, definition.Name), token: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task ClearAsync(SettingDefinition definition, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            return;
        }

        string userId = _currentUser.UserId;
        await _storeWriter.DeleteAsync(definition.Name, ProviderName, userId, cancellationToken).ConfigureAwait(false);
        await _cache.ExpireAsync(SettingCacheKey.Build(ProviderName, userId, definition.Name), token: cancellationToken).ConfigureAwait(false);
    }
}
