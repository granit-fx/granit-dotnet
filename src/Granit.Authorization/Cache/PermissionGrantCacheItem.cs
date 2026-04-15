namespace Granit.Authorization.Cache;

/// <summary>
/// Cache value for permission grant checks.
/// Public to allow serialization by <c>ICacheService&lt;T&gt;</c> across assembly boundaries.
/// Sealed record reduces deserialization attack surface when stored in distributed cache.
/// </summary>
public sealed record PermissionGrantCacheItem(bool IsGranted)
{
    /// <summary>Parameterless constructor for deserialization.</summary>
    public PermissionGrantCacheItem() : this(false) { }
}
