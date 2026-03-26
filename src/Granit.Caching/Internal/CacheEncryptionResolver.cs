using System.Collections.Concurrent;
using System.Reflection;
using Granit.Caching.Options;

namespace Granit.Caching.Internal;

/// <summary>
/// Resolves whether AES encryption should be applied for a given type.
/// Considers the <see cref="CacheEncryptedAttribute"/> attribute and the global <see cref="CachingOptions.EncryptValues"/> flag.
/// Results are cached per type to avoid repeated reflection.
/// </summary>
internal static class CacheEncryptionResolver
{
    private static readonly ConcurrentDictionary<Type, bool?> s_attributeCache = new();

    /// <summary>
    /// Determines whether values of type <paramref name="type"/> should be encrypted.
    /// </summary>
    /// <param name="type">The cache item type.</param>
    /// <param name="options">Global cache options.</param>
    /// <returns><c>true</c> if encryption should be applied.</returns>
    internal static bool ShouldEncrypt(Type type, CachingOptions options)
    {
        bool? attributeValue = s_attributeCache.GetOrAdd(
            type,
            static t => t.GetCustomAttribute<CacheEncryptedAttribute>()?.Encrypt);

        return attributeValue ?? options.EncryptValues;
    }
}
