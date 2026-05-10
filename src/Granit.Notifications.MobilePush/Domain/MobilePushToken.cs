using Granit.DataProtection;
using Granit.Domain;
using Granit.Encryption;

namespace Granit.Notifications.MobilePush.Domain;

/// <summary>
/// A registered mobile device push token (FCM / APNs) for a single user on a
/// single device. The aggregate enforces that the token plaintext and its
/// lookup-hash digest stay consistent — the digest is required at construction
/// because the encrypted plaintext column cannot serve equality lookups
/// (AES-CBC random IV → non-deterministic ciphertext).
/// </summary>
/// <remarks>
/// <para>
/// Identity is <c>(<see cref="DeviceTokenHash"/>, <see cref="IMultiTenant.TenantId"/>)</c>:
/// upsert keys on the hash, never on the encrypted plaintext.
/// </para>
/// <para>
/// <see cref="DeviceTokenHash"/> is computed via
/// <see cref="Granit.Notifications.MobilePush.Internal.IMobilePushTokenHasher"/>
/// (HMAC-SHA256 with a pepper distinct from the encryption key — separation of
/// secrets is the whole point of the lookup-hash pattern).
/// </para>
/// </remarks>
public sealed class MobilePushToken : CreationAuditedEntity, IMultiTenant
{
    private MobilePushToken() { }

    /// <summary>Creates a new mobile push token.</summary>
    /// <param name="userId">Identifier of the user the device belongs to.</param>
    /// <param name="deviceToken">FCM / APNs device token plaintext.</param>
    /// <param name="deviceTokenHash">Lookup digest of <paramref name="deviceToken"/> computed by
    /// <see cref="Granit.Notifications.MobilePush.Internal.IMobilePushTokenHasher"/>.</param>
    /// <param name="platform">iOS / Android.</param>
    /// <param name="tenantId">Optional tenant (multi-tenant deployments).</param>
    public static MobilePushToken Create(
        string userId,
        string deviceToken,
        string deviceTokenHash,
        MobilePlatform platform,
        Guid? tenantId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceTokenHash);

        return new MobilePushToken
        {
            UserId = userId,
            DeviceToken = deviceToken,
            DeviceTokenHash = deviceTokenHash,
            Platform = platform,
            TenantId = tenantId,
        };
    }

    /// <summary>The user the device belongs to.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>FCM / APNs device token plaintext. Encrypted at rest — equality
    /// lookups go through <see cref="DeviceTokenHash"/>.</summary>
    [SensitiveData(Level = Sensitivity.Restricted, Mode = SensitiveDataMode.Omit)]
    [Encrypted]
    public string DeviceToken { get; private set; } = string.Empty;

    /// <summary>HMAC-SHA256 lookup digest of <see cref="DeviceToken"/>
    /// (lower-case hex). Indexed with <see cref="TenantId"/> for the upsert /
    /// remove paths since AES-CBC ciphertext on <see cref="DeviceToken"/> is
    /// non-deterministic and cannot serve an exact-match query.</summary>
    public string DeviceTokenHash { get; private set; } = string.Empty;

    /// <summary>Device platform.</summary>
    public MobilePlatform Platform { get; private set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface for interceptor injection.</summary>
    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>Updates mutable attributes when re-registering a token already
    /// known to the system. Plaintext / hash are immutable — a new token value
    /// always means a new aggregate.</summary>
    public void Reassign(string userId, MobilePlatform platform)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        UserId = userId;
        Platform = platform;
    }
}
