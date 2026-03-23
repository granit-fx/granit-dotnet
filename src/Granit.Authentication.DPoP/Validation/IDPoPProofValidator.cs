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
}

/// <summary>
/// Result of DPoP proof validation.
/// </summary>
/// <param name="IsValid">Whether the proof is valid.</param>
/// <param name="JwkThumbprint">The base64url-encoded SHA-256 thumbprint of the proof's public key (RFC 7638). Null if invalid.</param>
/// <param name="Error">Error description if invalid. Null if valid.</param>
public sealed record DPoPValidationResult(bool IsValid, string? JwkThumbprint, string? Error)
{
    /// <summary>Creates a successful result.</summary>
    internal static DPoPValidationResult Success(string jwkThumbprint) => new(true, jwkThumbprint, null);

    /// <summary>Creates a failed result.</summary>
    internal static DPoPValidationResult Failure(string error) => new(false, null, error);
}
