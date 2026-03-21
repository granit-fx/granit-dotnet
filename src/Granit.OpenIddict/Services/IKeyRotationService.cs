namespace Granit.OpenIddict.Services;

/// <summary>
/// Manages the signing key rotation lifecycle.
/// </summary>
/// <remarks>
/// Called by the <c>openiddict-key-rotation</c> recurring job.
/// The implementation lives in <c>Granit.OpenIddict.EntityFrameworkCore</c>
/// and handles key generation, retirement, revocation, and cleanup.
/// </remarks>
public interface IKeyRotationService
{
    /// <summary>
    /// Executes a full rotation cycle: generate new keys if needed, retire old keys,
    /// revoke expired keys, and prune revoked keys.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A summary of actions taken during this rotation cycle.</returns>
    Task<KeyRotationResult> RotateAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Summary of a key rotation cycle.
/// </summary>
/// <param name="KeysGenerated">Number of new keys generated.</param>
/// <param name="KeysRetired">Number of keys transitioned to retired status.</param>
/// <param name="KeysRevoked">Number of keys transitioned to revoked status.</param>
/// <param name="KeysPruned">Number of revoked keys deleted from the database.</param>
public sealed record KeyRotationResult(
    int KeysGenerated,
    int KeysRetired,
    int KeysRevoked,
    int KeysPruned);
