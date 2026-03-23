#pragma warning disable GRSEC003 // Class handles client secrets — required for client_secret_post authentication

namespace Granit.Authentication.Oidc.ClientAuthentication.Internal;

/// <summary>
/// Applies <c>client_secret_post</c> authentication by including the client secret
/// as a POST body parameter.
/// </summary>
internal sealed class ClientSecretPostStrategy(string clientSecret) : IClientAuthenticationStrategy
{
    /// <inheritdoc/>
    public void Apply(Dictionary<string, string> parameters, string clientId, string tokenEndpoint)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        parameters[OidcConstants.Parameters.ClientSecret] = clientSecret;
    }
}

#pragma warning restore GRSEC003
