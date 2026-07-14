namespace Granit.Authentication.DPoP.Validation;

/// <summary>
/// Validates DPoP proof JWTs on incoming resource server requests (RFC 9449 §7).
/// </summary>
public interface IDPoPProofValidator
{
    /// <summary>
    /// Validates a DPoP proof JWT and returns the JWK Thumbprint of the proof's public key.
    /// </summary>
    /// <param name="proofJwt">The DPoP proof JWT from the <c>DPoP</c> header.</param>
    /// <param name="httpMethod">The HTTP method of the request (e.g., <c>"GET"</c>).</param>
    /// <param name="httpUri">The full request URL (scheme + host + path, no query).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="DPoPValidationResult"/> with <c>IsValid=true</c> and the JWK Thumbprint
    /// on success, or <c>IsValid=false</c> with an error description on failure.
    /// </returns>
    Task<DPoPValidationResult> ValidateAsync(
        string proofJwt,
        string httpMethod,
        string httpUri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a DPoP proof JWT presented at a protected resource together with an access token,
    /// additionally enforcing the <c>ath</c> access-token binding (RFC 9449 §4.3): the proof MUST
    /// carry an <c>ath</c> claim equal to <c>base64url(SHA-256(accessToken))</c>. Use this overload
    /// on the resource side; the parameterless-token overload is for the token endpoint, where no
    /// access token exists yet.
    /// </summary>
    /// <param name="proofJwt">The DPoP proof JWT from the <c>DPoP</c> header.</param>
    /// <param name="httpMethod">The HTTP method of the request (e.g., <c>"GET"</c>).</param>
    /// <param name="httpUri">The full request URL (scheme + host + path, no query).</param>
    /// <param name="accessToken">
    /// The access token the proof is presented with. When non-null, the proof's <c>ath</c> claim is
    /// required and must match its hash. When null, behaves like the token-endpoint overload.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<DPoPValidationResult> ValidateAsync(
        string proofJwt,
        string httpMethod,
        string httpUri,
        string? accessToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a fresh server nonce for the <c>DPoP-Nonce</c> response header (RFC 9449 §8).
    /// Returns <see langword="null"/> when nonce generation is not enabled.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<string?> GenerateNonceAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of DPoP proof validation.
/// </summary>
/// <param name="IsValid">Whether the proof is valid.</param>
/// <param name="JwkThumbprint">The base64url-encoded SHA-256 thumbprint of the proof's public key (RFC 7638). Null if invalid.</param>
/// <param name="Error">Error description if invalid. Null if valid.</param>
public sealed record DPoPValidationResult(bool IsValid, string? JwkThumbprint, string? Error)
{
    /// <summary>
    /// Server-issued nonce to return in the <c>DPoP-Nonce</c> response header (RFC 9449 §8).
    /// Present when nonce generation is enabled, regardless of validation outcome.
    /// </summary>
    public string? ServerNonce { get; init; }

    /// <summary>Creates a successful result.</summary>
    internal static DPoPValidationResult Success(string jwkThumbprint, string? serverNonce = null) =>
        new(true, jwkThumbprint, null) { ServerNonce = serverNonce };

    /// <summary>Creates a failed result.</summary>
    internal static DPoPValidationResult Failure(string error, string? serverNonce = null) =>
        new(false, null, error) { ServerNonce = serverNonce };
}
