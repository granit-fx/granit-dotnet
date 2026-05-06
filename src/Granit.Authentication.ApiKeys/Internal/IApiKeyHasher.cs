namespace Granit.Authentication.ApiKeys.Internal;

/// <summary>
/// Computes versioned API-key hashes. The version prefix lets the framework
/// add a server-side pepper (HMAC) to new keys without invalidating keys
/// hashed before the pepper was configured.
/// </summary>
internal interface IApiKeyHasher
{
    /// <summary>
    /// Computes the hash for a freshly generated key in the current preferred
    /// format. Format: <c>v2$&lt;hmac-sha256-hex&gt;</c> when a pepper is configured,
    /// <c>v1$&lt;sha256-hex&gt;</c> otherwise.
    /// </summary>
    string ComputeCurrentHash(string rawKey);

    /// <summary>
    /// Returns every hash that the framework still accepts for verification of
    /// a presented raw key, ordered from preferred to legacy. The auth handler
    /// queries the store with each in turn and returns on the first hit, so
    /// keys hashed before a pepper rotation remain usable until rotated.
    /// </summary>
    IReadOnlyList<string> ComputeCandidateHashes(string rawKey);
}
