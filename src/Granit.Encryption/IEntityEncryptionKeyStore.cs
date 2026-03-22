namespace Granit.Encryption;

/// <summary>
/// Provider-agnostic store for per-entity encryption keys.
/// Each entity instance gets its own AES-256 key, enabling crypto-shredding
/// (GDPR Art. 17) by deleting the key without touching database rows.
/// </summary>
/// <remarks>
/// Implementations must ensure key material is stored securely (e.g., Vault KV v2)
/// and that <see cref="DeleteKeyAsync"/> permanently destroys the key with no
/// recovery path (ISO 27001 A.10.1.2).
/// </remarks>
public interface IEntityEncryptionKeyStore
{
    /// <summary>
    /// Returns the AES-256 key for the given entity, creating one if it does not exist.
    /// </summary>
    /// <param name="entityType">Logical entity type name (e.g., <c>"Patient"</c>).</param>
    /// <param name="entityId">Entity identifier (typically <c>Guid.ToString()</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>32-byte AES-256 key.</returns>
    Task<byte[]> GetOrCreateKeyAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the AES-256 key for the given entity, or <c>null</c> if the key
    /// does not exist (e.g., after crypto-shredding).
    /// </summary>
    Task<byte[]?> GetKeyAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently destroys the per-entity key. This is the crypto-shredding operation:
    /// all ciphertext encrypted with this key becomes permanently unreadable.
    /// </summary>
    Task DeleteKeyAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a per-entity key exists without retrieving the key material.
    /// </summary>
    Task<bool> KeyExistsAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);
}
