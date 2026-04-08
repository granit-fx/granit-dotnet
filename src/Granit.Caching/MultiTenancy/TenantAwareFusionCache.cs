using Granit.MultiTenancy;
using Microsoft.Extensions.Caching.Distributed;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Backplane;
using ZiggyCreatures.Caching.Fusion.Events;
using ZiggyCreatures.Caching.Fusion.Locking.Distributed;
using ZiggyCreatures.Caching.Fusion.Plugins;
using ZiggyCreatures.Caching.Fusion.Serialization;

namespace Granit.Caching.MultiTenancy;

/// <summary>
/// Scoped decorator around <see cref="IFusionCache"/> that automatically prefixes
/// all cache keys and tags with the current tenant identifier, preventing
/// cross-tenant cache pollution on shared Redis instances.
/// </summary>
/// <remarks>
/// <para>Key format: <c>t:{tenantId:N}:{originalKey}</c> when a tenant is active,
/// or <c>t:host:{originalKey}</c> in host context (no active tenant).</para>
/// <para>The inner <see cref="IFusionCache"/> remains a singleton — only key
/// prefixing is per-scope. All infrastructure operations (setup, plugins) are
/// delegated unchanged.</para>
/// </remarks>
internal sealed class TenantAwareFusionCache(
    IFusionCache inner,
    ICurrentTenant currentTenant) : IFusionCache
{
    /// <summary>Keyed service key for the raw (non-decorated) <see cref="IFusionCache"/> singleton.</summary>
    internal const string RawCacheKey = "__granit_raw_cache__";

    private string TenantSegment => currentTenant.IsAvailable
        ? currentTenant.Id!.Value.ToString("N")
        : "host";

    private string PrefixKey(string key) => $"t:{TenantSegment}:{key}";

    private string PrefixTag(string tag) => $"t:{TenantSegment}:{tag}";

    private IEnumerable<string>? PrefixTags(IEnumerable<string>? tags) =>
        tags?.Select(PrefixTag);

    // ── Properties (passthrough) ──────────────────────────────────────

    public string CacheName => inner.CacheName;
    public string InstanceId => inner.InstanceId;
    public FusionCacheEntryOptions DefaultEntryOptions => inner.DefaultEntryOptions;
    public FusionCacheEntryOptionsProvider? DefaultEntryOptionsProvider => inner.DefaultEntryOptionsProvider;
    public bool HasDistributedCache => inner.HasDistributedCache;
    public IDistributedCache? DistributedCache => inner.DistributedCache;
    public bool HasBackplane => inner.HasBackplane;
    public IFusionCacheBackplane? Backplane => inner.Backplane;
    public bool HasDistributedLocker => inner.HasDistributedLocker;
    public FusionCacheEventsHub Events => inner.Events;

    // ── Factory & Options (passthrough) ───────────────────────────────

    public FusionCacheEntryOptions CreateEntryOptions(
        Action<FusionCacheEntryOptions>? setupAction = null,
        TimeSpan? duration = null) =>
        inner.CreateEntryOptions(setupAction, duration);

    // ── GetOrSet (key-prefixed) ───────────────────────────────────────

    public ValueTask<TValue> GetOrSetAsync<TValue>(
        string key,
        Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, Task<TValue>> factory,
        MaybeValue<TValue> failSafeDefaultValue = default,
        FusionCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken token = default) =>
        inner.GetOrSetAsync(PrefixKey(key), factory, failSafeDefaultValue, options, PrefixTags(tags), token);

    public TValue GetOrSet<TValue>(
        string key,
        Func<FusionCacheFactoryExecutionContext<TValue>, CancellationToken, TValue> factory,
        MaybeValue<TValue> failSafeDefaultValue = default,
        FusionCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken token = default) =>
        inner.GetOrSet(PrefixKey(key), factory, failSafeDefaultValue, options, PrefixTags(tags), token);

    public ValueTask<TValue> GetOrSetAsync<TValue>(
        string key,
        TValue defaultValue,
        FusionCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken token = default) =>
        inner.GetOrSetAsync(PrefixKey(key), defaultValue, options, PrefixTags(tags), token);

    public TValue GetOrSet<TValue>(
        string key,
        TValue defaultValue,
        FusionCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken token = default) =>
        inner.GetOrSet(PrefixKey(key), defaultValue, options, PrefixTags(tags), token);

    // ── GetOrDefault (key-prefixed) ───────────────────────────────────

    public ValueTask<TValue?> GetOrDefaultAsync<TValue>(
        string key,
        TValue? defaultValue = default,
        FusionCacheEntryOptions? options = null,
        CancellationToken token = default) =>
        inner.GetOrDefaultAsync(PrefixKey(key), defaultValue, options, token);

    public TValue? GetOrDefault<TValue>(
        string key,
        TValue? defaultValue = default,
        FusionCacheEntryOptions? options = null,
        CancellationToken token = default) =>
        inner.GetOrDefault(PrefixKey(key), defaultValue, options, token);

    // ── TryGet (key-prefixed) ─────────────────────────────────────────

    public ValueTask<MaybeValue<TValue>> TryGetAsync<TValue>(
        string key,
        FusionCacheEntryOptions? options = null,
        CancellationToken token = default) =>
        inner.TryGetAsync<TValue>(PrefixKey(key), options, token);

    public MaybeValue<TValue> TryGet<TValue>(
        string key,
        FusionCacheEntryOptions? options = null,
        CancellationToken token = default) =>
        inner.TryGet<TValue>(PrefixKey(key), options, token);

    // ── Set (key-prefixed) ────────────────────────────────────────────

    public ValueTask SetAsync<TValue>(
        string key,
        TValue value,
        FusionCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken token = default) =>
        inner.SetAsync(PrefixKey(key), value, options, PrefixTags(tags), token);

    public void Set<TValue>(
        string key,
        TValue value,
        FusionCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken token = default) =>
        inner.Set(PrefixKey(key), value, options, PrefixTags(tags), token);

    // ── Remove (key-prefixed) ─────────────────────────────────────────

    public ValueTask RemoveAsync(
        string key,
        FusionCacheEntryOptions? options = null,
        CancellationToken token = default) =>
        inner.RemoveAsync(PrefixKey(key), options, token);

    public void Remove(
        string key,
        FusionCacheEntryOptions? options = null,
        CancellationToken token = default) =>
        inner.Remove(PrefixKey(key), options, token);

    // ── Expire (key-prefixed) ─────────────────────────────────────────

    public ValueTask ExpireAsync(
        string key,
        FusionCacheEntryOptions? options = null,
        CancellationToken token = default) =>
        inner.ExpireAsync(PrefixKey(key), options, token);

    public void Expire(
        string key,
        FusionCacheEntryOptions? options = null,
        CancellationToken token = default) =>
        inner.Expire(PrefixKey(key), options, token);

    // ── RemoveByTag (tag-prefixed) ────────────────────────────────────

    public ValueTask RemoveByTagAsync(
        string tag,
        FusionCacheEntryOptions? options = null,
        CancellationToken token = default) =>
        inner.RemoveByTagAsync(PrefixTag(tag), options, token);

    public ValueTask RemoveByTagAsync(
        IEnumerable<string> tags,
        FusionCacheEntryOptions? options = null,
        CancellationToken token = default) =>
        inner.RemoveByTagAsync(tags.Select(PrefixTag), options, token);

    public void RemoveByTag(
        string tag,
        FusionCacheEntryOptions? options = null,
        CancellationToken token = default) =>
        inner.RemoveByTag(PrefixTag(tag), options, token);

    public void RemoveByTag(
        IEnumerable<string> tags,
        FusionCacheEntryOptions? options = null,
        CancellationToken token = default) =>
        inner.RemoveByTag(tags.Select(PrefixTag), options, token);

    // ── Clear (passthrough — clears everything, tenant-agnostic) ──────

    public ValueTask ClearAsync(
        bool allowFailSafe = true,
        FusionCacheEntryOptions? options = null,
        CancellationToken token = default) =>
        inner.ClearAsync(allowFailSafe, options, token);

    public void Clear(
        bool allowFailSafe = true,
        FusionCacheEntryOptions? options = null,
        CancellationToken token = default) =>
        inner.Clear(allowFailSafe, options, token);

    // ── Infrastructure setup (passthrough) ────────────────────────────

    public IFusionCache SetupSerializer(IFusionCacheSerializer serializer) =>
        inner.SetupSerializer(serializer);

    public IFusionCache SetupDistributedCache(IDistributedCache distributedCache) =>
        inner.SetupDistributedCache(distributedCache);

    public IFusionCache SetupDistributedCache(
        IDistributedCache distributedCache,
        IFusionCacheSerializer serializer) =>
        inner.SetupDistributedCache(distributedCache, serializer);

    public IFusionCache RemoveDistributedCache() =>
        inner.RemoveDistributedCache();

    public IFusionCache SetupBackplane(IFusionCacheBackplane backplane) =>
        inner.SetupBackplane(backplane);

    public IFusionCache RemoveBackplane() =>
        inner.RemoveBackplane();

    public IFusionCache SetupDistributedLocker(IFusionCacheDistributedLocker distributedLocker) =>
        inner.SetupDistributedLocker(distributedLocker);

    public IFusionCache RemoveDistributedLocker() =>
        inner.RemoveDistributedLocker();

    // ── Plugins (passthrough) ─────────────────────────────────────────

    public void AddPlugin(IFusionCachePlugin plugin) =>
        inner.AddPlugin(plugin);

    public bool RemovePlugin(IFusionCachePlugin plugin) =>
        inner.RemovePlugin(plugin);

    // ── Dispose (passthrough) ─────────────────────────────────────────

    public void Dispose()
    {
        // Do NOT dispose the inner cache — it's a singleton shared across all scopes.
    }
}
