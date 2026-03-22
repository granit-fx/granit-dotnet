using System.Collections.ObjectModel;

namespace Granit.Identity.Federated;

/// <summary>
/// Immutable snapshot of user data returned by federated identity providers
/// (Keycloak, Entra ID, Cognito, Google Cloud).
/// </summary>
/// <remarks>
/// Replaces the legacy <c>IdentityUser</c> sealed record. Implements
/// <see cref="IIdentityUser"/> for unified consumption across the framework.
/// </remarks>
/// <param name="UserId">The provider's native user identifier.</param>
/// <param name="Username">Login name.</param>
/// <param name="Email">Email address.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="Enabled">Whether the user account is active.</param>
/// <param name="ExtraProperties">
/// Provider-specific attributes (e.g., Keycloak user attributes, Entra ID extension attributes).
/// <see langword="null"/> when the provider does not return attributes or the user has none.
/// </param>
public sealed record FederatedIdentityUser(
    string UserId,
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    bool Enabled,
    IReadOnlyDictionary<string, string>? ExtraProperties = null) : IIdentityUser
{
    private static readonly IReadOnlyDictionary<string, string> EmptyProperties =
        ReadOnlyDictionary<string, string>.Empty;

    /// <inheritdoc/>
    IReadOnlyDictionary<string, string> IIdentityUser.ExtraProperties =>
        ExtraProperties ?? EmptyProperties;
}
