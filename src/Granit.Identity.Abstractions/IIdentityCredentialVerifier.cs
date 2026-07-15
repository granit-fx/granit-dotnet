namespace Granit.Identity;

/// <summary>
/// Verifies user credentials against the identity provider.
/// Used for re-authentication before sensitive operations.
/// </summary>
public interface IIdentityCredentialVerifier
{
    /// <summary>Verifies the given username/password pair against the identity provider.</summary>
    Task<bool> VerifyUserCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
