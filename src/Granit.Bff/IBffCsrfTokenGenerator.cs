namespace Granit.Bff;

/// <summary>
/// Generates and validates CSRF tokens for BFF sessions.
/// Uses a double-submit pattern: token is issued by the server and sent back
/// via <c>X-CSRF-Token</c> header on mutating requests.
/// </summary>
public interface IBffCsrfTokenGenerator
{
    /// <summary>Generates a CSRF token bound to the given session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>A CSRF token string.</returns>
    string Generate(string sessionId);

    /// <summary>Validates a CSRF token against the given session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="token">The token to validate.</param>
    /// <returns><c>true</c> if the token is valid; otherwise <c>false</c>.</returns>
    bool Validate(string sessionId, string token);
}
