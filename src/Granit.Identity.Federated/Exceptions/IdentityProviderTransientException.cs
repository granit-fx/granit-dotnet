namespace Granit.Identity.Federated.Exceptions;

/// <summary>
/// Thrown by a federated identity provider on a transient upstream fault (5xx, timeout,
/// connection reset) — a failure that a retry might resolve, distinct from an authorization
/// or not-found condition. The graceful-degradation decorator degrades read operations on
/// this category (returns an empty/stale result) so a blip does not fail the whole request.
/// </summary>
public sealed class IdentityProviderTransientException : IdentityProviderException
{
    public IdentityProviderTransientException(string providerName, string operation, Exception? innerException = null)
        : base(
            IdentityProviderFailureCategory.Transient,
            providerName,
            operation,
            $"Identity provider '{providerName}' failed operation '{operation}' with a transient upstream error.",
            innerException)
    {
    }
}
