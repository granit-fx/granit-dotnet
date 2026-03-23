using Granit.Bff.Options;

namespace Granit.Bff.ClientAssertion;

/// <summary>
/// Shared helper for applying client authentication parameters to token endpoint requests.
/// Used by both <c>Granit.Bff.Endpoints</c> (login/PAR) and <c>Granit.Bff.Yarp</c> (refresh).
/// </summary>
public static class BffClientAuthentication
{
    /// <summary>
    /// Adds client authentication parameters to a token endpoint request dictionary.
    /// </summary>
    /// <param name="parameters">The form parameters dictionary.</param>
    /// <param name="frontend">The frontend configuration.</param>
    /// <param name="tokenEndpoint">The token endpoint URL (used as <c>aud</c> for JWT assertions).</param>
    /// <param name="assertionService">The client assertion service.</param>
    public static void Apply(
        Dictionary<string, string> parameters,
        BffFrontendOptions frontend,
        string tokenEndpoint,
        IBffClientAssertionService assertionService)
    {
        if (frontend.ClientAuthenticationMethod == BffClientAuthenticationMethod.PrivateKeyJwt)
        {
            parameters["client_assertion"] = assertionService.CreateAssertion(
                frontend.ClientSigningKeyJwk!, frontend.ClientId, tokenEndpoint);
            parameters["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
        }
        else
        {
            parameters["client_secret"] = frontend.ClientSecret;
        }
    }
}
