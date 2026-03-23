namespace Granit.Authentication.Oidc.ClientAuthentication;

/// <summary>
/// Applies client authentication parameters to a token endpoint request.
/// Implementations handle specific methods such as <c>client_secret_post</c> or <c>private_key_jwt</c>.
/// </summary>
public interface IClientAuthenticationStrategy
{
    /// <summary>
    /// Applies client authentication parameters to the given parameter dictionary.
    /// </summary>
    /// <param name="parameters">The mutable form parameter dictionary for the token endpoint request.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="tokenEndpoint">The token endpoint URL (used as audience for JWT assertions).</param>
    void Apply(Dictionary<string, string> parameters, string clientId, string tokenEndpoint);
}
