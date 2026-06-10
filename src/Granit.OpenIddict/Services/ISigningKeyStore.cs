using Granit.OpenIddict.Domain;

namespace Granit.OpenIddict.Services;

/// <summary>
/// Store for persisted signing and encryption keys.
/// </summary>
public interface ISigningKeyStore
{
    /// <summary>
    /// Returns all keys with the specified statuses, ordered by creation date descending.
    /// </summary>
    Task<IReadOnlyList<SigningKey>> GetKeysAsync(
        SigningKeyStatus[] statuses,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all keys with the specified statuses, ordered by creation date descending.
    /// </summary>
    /// <remarks>Convenience overload without <see cref="CancellationToken"/>.</remarks>
    Task<IReadOnlyList<SigningKey>> GetKeysAsync(
        params SigningKeyStatus[] statuses);

    /// <summary>
    /// Returns the currently active key for the specified type, or null if none exists.
    /// </summary>
    Task<SigningKey?> GetActiveKeyAsync(string keyType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new key.
    /// </summary>
    Task CreateAsync(SigningKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing key (e.g., status change on rotation).
    /// </summary>
    /// <returns>
    /// <c>true</c> if the update was applied; <c>false</c> if it lost an optimistic-concurrency
    /// race (another rotation already transitioned this key). Callers must treat <c>false</c> as
    /// "another runner won" and abort the dependent step rather than retrying — this is the
    /// guard against a double key rotation minting two active keys across replicas.
    /// </returns>
    Task<bool> UpdateAsync(SigningKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes keys older than the specified cutoff (cleanup of revoked keys).
    /// </summary>
    Task<int> PruneRevokedAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}
