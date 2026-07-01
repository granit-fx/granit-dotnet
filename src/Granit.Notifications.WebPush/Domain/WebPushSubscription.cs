using Granit.DataProtection;
using Granit.Domain;
using Granit.Encryption;

namespace Granit.Notifications.WebPush.Domain;

/// <summary>
/// A persisted W3C Push API browser subscription for a single user on a single
/// browser/device. The <see cref="Endpoint"/> is the natural key: it is a unique,
/// opaque push-service URL and is queryable for upsert / removal. The encryption
/// key material (<see cref="P256dh"/>, <see cref="Auth"/>) is encrypted at rest —
/// it is never used for equality lookups.
/// </summary>
/// <remarks>
/// Identity is <c>(<see cref="Endpoint"/>, <see cref="IMultiTenant.TenantId"/>)</c>: a browser
/// push endpoint is globally unique, so re-subscribing the same endpoint upserts the row rather
/// than creating a duplicate.
/// </remarks>
public sealed class WebPushSubscription : CreationAuditedEntity, IMultiTenant
{
    private WebPushSubscription() { }

    /// <summary>Creates a new browser push subscription.</summary>
    /// <param name="userId">Identifier of the user the browser belongs to.</param>
    /// <param name="endpoint">Push service endpoint URL (unique, opaque).</param>
    /// <param name="p256dh">P-256 DH public key (Base64 URL-safe).</param>
    /// <param name="auth">Authentication secret (Base64 URL-safe).</param>
    /// <param name="expirationTime">Optional subscription expiration (Unix epoch ms).</param>
    /// <param name="tenantId">Optional tenant (multi-tenant deployments).</param>
    public static WebPushSubscription Create(
        string userId,
        string endpoint,
        string p256dh,
        string auth,
        long? expirationTime = null,
        Guid? tenantId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(p256dh);
        ArgumentException.ThrowIfNullOrWhiteSpace(auth);

        return new WebPushSubscription
        {
            UserId = userId,
            Endpoint = endpoint,
            P256dh = p256dh,
            Auth = auth,
            ExpirationTime = expirationTime,
            TenantId = tenantId,
        };
    }

    /// <summary>The user the browser belongs to.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Push service endpoint URL — the natural, queryable key.</summary>
    public string Endpoint { get; private set; } = string.Empty;

    /// <summary>P-256 DH public key (Base64 URL-safe). Encrypted at rest.</summary>
    [SensitiveData(Level = Sensitivity.Restricted, Mode = SensitiveDataMode.Omit)]
    [Encrypted]
    public string P256dh { get; private set; } = string.Empty;

    /// <summary>Authentication secret (Base64 URL-safe). Encrypted at rest.</summary>
    [SensitiveData(Level = Sensitivity.Restricted, Mode = SensitiveDataMode.Omit)]
    [Encrypted]
    public string Auth { get; private set; } = string.Empty;

    /// <summary>Subscription expiration (Unix epoch ms), or null if it never expires.</summary>
    public long? ExpirationTime { get; private set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface for interceptor injection.</summary>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Refreshes the key material and owner when the same endpoint re-subscribes.</summary>
    public void Update(string userId, string p256dh, string auth, long? expirationTime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(p256dh);
        ArgumentException.ThrowIfNullOrWhiteSpace(auth);

        UserId = userId;
        P256dh = p256dh;
        Auth = auth;
        ExpirationTime = expirationTime;
    }
}
