namespace Granit.Caching;

/// <summary>
/// Overrides the cache name computed by convention for this type.
/// By convention, <c>UserCacheItem</c> → <c>"User"</c> (the "CacheItem" suffix is stripped).
/// Use this attribute to customise the name and therefore the middle segment of the composite key.
/// </summary>
/// <example>
/// <code>
/// [CacheName("Patient")]
/// public sealed class PatientSummaryCacheItem { }
/// // Generated key: dd:Patient:{userKey}
/// </code>
/// </example>
/// <param name="name">Name to use as the middle segment of the composite key.</param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class CacheNameAttribute(string name) : Attribute
{
    /// <summary>Custom cache name.</summary>
    public string Name { get; } = name;
}
