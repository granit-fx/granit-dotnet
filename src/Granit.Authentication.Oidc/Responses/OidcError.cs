namespace Granit.Authentication.Oidc.Responses;

/// <summary>
/// Represents an OAuth 2.0 error response (RFC 6749 §5.2).
/// </summary>
/// <param name="Error">The error code (e.g., <c>"invalid_grant"</c>, <c>"invalid_client"</c>).</param>
/// <param name="ErrorDescription">Optional human-readable description of the error.</param>
/// <param name="ErrorUri">Optional URI identifying a human-readable web page with error information.</param>
public sealed record OidcError(string Error, string? ErrorDescription = null, string? ErrorUri = null);
