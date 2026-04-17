using Granit.Exceptions;

namespace Granit.Vault.Exceptions;

/// <summary>
/// Thrown when the requested secret does not exist in the configured vault.
/// Maps to <c>404 Not Found</c> when surfaced through HTTP.
/// </summary>
/// <remarks>
/// Inherits the OWASP-compliant generic message from <see cref="NotFoundException"/>.
/// The secret name is attached as a property (<see cref="SecretName"/>) for structured
/// logging but is NOT included in the default message to avoid information disclosure.
/// </remarks>
public sealed class SecretNotFoundException : NotFoundException
{
    /// <summary>Name of the secret that was not found. Use for structured logs, NOT for user-facing messages.</summary>
    public string SecretName { get; }

    /// <summary>Provider-specific version identifier if the request targeted a specific version.</summary>
    public string? Version { get; }

    /// <summary>Initializes a new instance.</summary>
    public SecretNotFoundException(string secretName, string? version = null, Exception? innerException = null)
        : base("The requested secret was not found.", innerException)
    {
        SecretName = secretName;
        Version = version;
    }
}
