using System.Collections.Concurrent;
using System.Reflection;

namespace Granit.Caching.Internal;

/// <summary>
/// Provides the cache name for a type based on convention or the <see cref="CacheNameAttribute"/> attribute.
/// Resolved names are cached in memory to avoid repeated reflection.
/// </summary>
/// <remarks>
/// Convention: the <c>"CacheItem"</c> suffix is stripped from the type name.
/// Examples: <c>UserCacheItem</c> → <c>"User"</c>, <c>PatientRecord</c> → <c>"PatientRecord"</c>
/// Override with <c>[CacheName("name")]</c> on the class.
/// </remarks>
internal static class CacheNameProvider
{
    private static readonly ConcurrentDictionary<Type, string> _cache = new();
    private const string CacheItemSuffix = "CacheItem";

    /// <summary>
    /// Returns the cache name for the specified type.
    /// </summary>
    internal static string GetCacheName(Type type) =>
        _cache.GetOrAdd(type, ResolveNameFromType);

    private static string ResolveNameFromType(Type type)
    {
        CacheNameAttribute? attribute = type.GetCustomAttribute<CacheNameAttribute>();

        if (attribute is not null)
        {
            return attribute.Name;
        }

        string name = type.Name;

        return name.EndsWith(CacheItemSuffix, StringComparison.Ordinal)
            ? name[..^CacheItemSuffix.Length]
            : name;
    }
}
