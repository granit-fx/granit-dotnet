namespace Granit.Notifications.MobilePush.Internal;

/// <summary>
/// Computes a deterministic lookup hash for a mobile push device token so the
/// EF Core store can upsert and look up tokens without decrypting every row.
/// </summary>
/// <remarks>
/// <para>
/// <c>Granit.Notifications.EntityFrameworkCore.Entities.MobilePushTokenEntity.DeviceToken</c>
/// is encrypted at rest via <c>Granit.Encryption.EncryptedAttribute</c>.
/// AES-CBC ciphertext is non-deterministic (random IV), so equality lookups on
/// the encrypted column are impossible — a parallel deterministic-hash column
/// (<c>DeviceTokenHash = HMAC-SHA256(pepper, deviceToken)</c>) is indexed for
/// the upsert / remove paths instead. Same pattern as
/// <c>Granit.Identity.IUserLookupHasher</c>.
/// </para>
/// <para>
/// The pepper MUST be distinct from the encryption key so the two can be
/// rotated independently. Sharing a key would leak re-identification risk
/// into the encryption domain.
/// </para>
/// </remarks>
internal interface IMobilePushTokenHasher
{
    /// <summary>
    /// Computes the deterministic lookup hash for a device token. Returns
    /// <see langword="null"/> when <paramref name="deviceToken"/> is
    /// <see langword="null"/> or empty so the caller can persist it verbatim.
    /// </summary>
    string? ComputeHash(string? deviceToken);
}
