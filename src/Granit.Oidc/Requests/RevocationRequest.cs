namespace Granit.Oidc.Requests;

/// <summary>
/// Represents an OAuth 2.0 token revocation request (RFC 7009).
/// </summary>
public sealed record RevocationRequest
{
    /// <summary>
    /// The token to revoke.
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Optional hint about the type of the token being revoked
    /// (e.g., <c>"access_token"</c> or <c>"refresh_token"</c>).
    /// </summary>
    public string? TokenTypeHint { get; init; }

    /// <summary>
    /// The client identifier.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Additional parameters to include in the revocation request.
    /// </summary>
    public Dictionary<string, string> AdditionalParameters { get; init; } = [];
}
