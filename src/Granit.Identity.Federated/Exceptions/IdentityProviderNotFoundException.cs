namespace Granit.Identity.Federated.Exceptions;

/// <summary>
/// Thrown by a federated identity provider when the targeted resource (user, client, group)
/// does not exist upstream (HTTP 404). Distinct from <see cref="IdentityProviderUnauthorizedException"/>
/// (an IAM fault) so a genuinely absent user is not misread as an outage.
/// </summary>
public sealed class IdentityProviderNotFoundException : IdentityProviderException
{
    public IdentityProviderNotFoundException(string providerName, string operation, Exception? innerException = null)
        : base(
            IdentityProviderFailureCategory.NotFound,
            providerName,
            operation,
            $"Identity provider '{providerName}' could not find the target of operation '{operation}'.",
            innerException)
    {
    }
}
