namespace Granit.AI.Tenancy;

/// <summary>Outcome of <see cref="AIEndpointValidator.Validate(string?, AIEndpointPolicy)"/>.</summary>
/// <param name="IsValid">True when the endpoint is acceptable for the supplied policy.</param>
/// <param name="Error">Machine-readable error code when <see cref="IsValid"/> is false.</param>
/// <param name="ResolvedHost">
/// The IDN-normalised hostname (or IP literal) extracted from the URL — useful for the
/// <see cref="GranitSafeConnectCallback"/> to revalidate connection-time IPs against the same policy.
/// </param>
public sealed record AIEndpointValidationResult(bool IsValid, string? Error = null, string? ResolvedHost = null)
{
    /// <summary>Successful validation.</summary>
    public static AIEndpointValidationResult Ok(string resolvedHost) =>
        new(IsValid: true, Error: null, ResolvedHost: resolvedHost);

    /// <summary>Validation failure with a machine-readable error code.</summary>
    public static AIEndpointValidationResult Fail(string error) =>
        new(IsValid: false, Error: error, ResolvedHost: null);
}
