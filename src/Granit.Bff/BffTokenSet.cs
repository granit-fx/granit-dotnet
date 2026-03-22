namespace Granit.Bff;

/// <summary>
/// Represents a set of OIDC tokens stored server-side for a BFF session.
/// Tokens never leave the server — the browser only holds a session cookie.
/// </summary>
#pragma warning disable GRSEC003 // Record contains token properties — stored server-side only
public sealed record BffTokenSet(
    string AccessToken,
    string? RefreshToken,
    string? IdToken,
    DateTimeOffset ExpiresAt);
#pragma warning restore GRSEC003
