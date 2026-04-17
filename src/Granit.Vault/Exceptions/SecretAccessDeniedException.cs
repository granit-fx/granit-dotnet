using Granit.Exceptions;

namespace Granit.Vault.Exceptions;

/// <summary>
/// Thrown when the vault denies access to the requested secret (401/403).
/// Maps to <c>403 Forbidden</c> when surfaced through HTTP.
/// </summary>
/// <remarks>
/// Inherits the OWASP-compliant generic message from <see cref="ForbiddenException"/>.
/// The secret name is attached as a property (<see cref="SecretName"/>) for structured
/// logging but is NOT included in the default message to avoid information disclosure.
/// </remarks>
public sealed class SecretAccessDeniedException : ForbiddenException
{
    /// <summary>Name of the secret whose access was denied. Use for structured logs only.</summary>
    public string SecretName { get; }

    /// <summary>Initializes a new instance.</summary>
    public SecretAccessDeniedException(string secretName, Exception? innerException = null)
        : base("Access to the requested secret is forbidden.", innerException)
    {
        SecretName = secretName;
    }
}
