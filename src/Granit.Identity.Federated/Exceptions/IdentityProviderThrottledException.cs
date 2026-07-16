namespace Granit.Identity.Federated.Exceptions;

/// <summary>
/// Thrown by a federated identity provider when the upstream API rate-limits the request
/// (HTTP 429 / <c>TooManyRequests</c>). Carries the server-advertised <see cref="RetryAfter"/>
/// when available so callers and background schedulers can back off intelligently.
/// </summary>
public sealed class IdentityProviderThrottledException : IdentityProviderException
{
    public IdentityProviderThrottledException(
        string providerName, string operation, TimeSpan? retryAfter = null, Exception? innerException = null)
        : base(
            IdentityProviderFailureCategory.Throttled,
            providerName,
            operation,
            $"Identity provider '{providerName}' throttled operation '{operation}'.",
            innerException)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>The server-advertised back-off delay, when the upstream supplied one.</summary>
    public TimeSpan? RetryAfter { get; }
}
