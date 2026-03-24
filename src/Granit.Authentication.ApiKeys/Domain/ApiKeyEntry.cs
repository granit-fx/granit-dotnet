using Granit.Authentication.ApiKeys.Events;
using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.Authentication.ApiKeys.Domain;

/// <summary>
/// Represents an API key with its metadata, permissions, and lifecycle state.
/// </summary>
public sealed class ApiKeyEntry : FullAuditedAggregateRoot, IMultiTenant
{
    // Parameterless constructor required by EF Core materializer.
    private ApiKeyEntry() { }

    /// <summary>
    /// Creates a new active <see cref="ApiKeyEntry"/>.
    /// </summary>
    public static ApiKeyEntry Create(
        Guid id,
        string name,
        ApiKeyType type,
        string environment,
        string hashedKey,
        string prefix,
        string lastFourChars,
        Guid? tenantId = null)
    {
        var entry = new ApiKeyEntry
        {
            Id = id,
            Name = name,
            Type = type,
            Environment = environment,
            HashedKey = hashedKey,
            Prefix = prefix,
            LastFourChars = lastFourChars,
            TenantId = tenantId,
        };

        entry.AddDistributedEvent(new ApiKeyCreatedEto(entry.Id, entry.Name, entry.Type));
        return entry;
    }

    /// <summary>Display name of the API key (e.g., "Partenaire Labo X").</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Type of the API key (Secret, Publishable, Webhook, Ephemeral).</summary>
    public ApiKeyType Type { get; private set; }

    /// <summary>Target environment (<c>live</c>, <c>test</c>, <c>dev</c>).</summary>
    public string Environment { get; private set; } = string.Empty;

    /// <summary>SHA-256 hash of the raw secret. The raw secret is never stored.</summary>
    public string HashedKey { get; private set; } = string.Empty;

    /// <summary>Prefix of the key for display purposes (e.g., <c>gk_live_sk_</c>).</summary>
    public string Prefix { get; private set; } = string.Empty;

    /// <summary>Last four characters of the raw secret for identification.</summary>
    public string LastFourChars { get; private set; } = string.Empty;

    /// <summary>Permissions granted to this API key (e.g., <c>["MyApp.Patients.Read"]</c>).</summary>
    public List<string> Permissions { get; private set; } = [];

    /// <summary>Allowed CIDR ranges for IP whitelisting. Empty means no restriction.</summary>
    public List<string> AllowedCidrs { get; private set; } = [];

    /// <summary>Expiration date. <c>null</c> means the key does not expire.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>Timestamp of the last API call using this key.</summary>
    public DateTimeOffset? LastUsedAt { get; private set; }

    /// <summary>Timestamp when the key was revoked. <c>null</c> if still active.</summary>
    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Controls caching behavior for this key.</summary>
    public CacheBehavior CacheBehavior { get; private set; }

    // IMultiTenant — explicit interface for private set encapsulation
    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <inheritdoc/>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>
    /// Revokes the API key and emits an <see cref="ApiKeyRevokedEto"/> integration event
    /// for cross-instance cache invalidation via Wolverine outbox.
    /// </summary>
    public void Revoke(DateTimeOffset revokedAt)
    {
        RevokedAt = revokedAt;
        AddDistributedEvent(new ApiKeyRevokedEto(Id, HashedKey));
    }

    /// <summary>
    /// Records that this key was used for an API call.
    /// </summary>
    internal void RecordUsage(DateTimeOffset usedAt)
    {
        LastUsedAt = usedAt;
        AddDistributedEvent(new ApiKeyUsedEto(Id, Prefix, usedAt));
    }

    /// <summary>
    /// Marks this key as expired and emits an <see cref="ApiKeyExpiredEvent"/> domain event.
    /// Called by the authentication handler when an expired key is detected.
    /// </summary>
    internal void MarkAsExpired()
    {
        if (ExpiresAt is null)
        {
            return;
        }

        AddDomainEvent(new ApiKeyExpiredEvent(Id, Prefix, ExpiresAt.Value));
    }

    /// <summary>
    /// Updates the permissions granted to this key.
    /// </summary>
    public void UpdatePermissions(List<string> permissions)
    {
        Permissions = permissions;
        AddDistributedEvent(new ApiKeyScopesUpdatedEto(Id, HashedKey));
    }

    /// <summary>
    /// Updates the allowed CIDR ranges for IP whitelisting.
    /// </summary>
    public void UpdateAllowedCidrs(List<string> cidrs) =>
        AllowedCidrs = cidrs;

    /// <summary>
    /// Sets the expiration date.
    /// </summary>
    public void SetExpiration(DateTimeOffset? expiresAt) =>
        ExpiresAt = expiresAt;

    /// <summary>
    /// Sets the caching behavior.
    /// </summary>
    public void SetCacheBehavior(CacheBehavior cacheBehavior) =>
        CacheBehavior = cacheBehavior;
}
