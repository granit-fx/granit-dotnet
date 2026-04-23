using Granit.Oidc.Requests;

namespace Granit.Oidc.Internal;

/// <summary>
/// Converts typed OIDC request objects to form-encoded parameter dictionaries and HTTP content.
/// </summary>
internal static class TokenRequestEncoder
{
    /// <summary>
    /// Encodes a <see cref="TokenRequest"/> as <see cref="FormUrlEncodedContent"/>.
    /// </summary>
    internal static FormUrlEncodedContent Encode(TokenRequest request)
    {
        Dictionary<string, string> parameters = ToParameters(request);
        return new FormUrlEncodedContent(parameters);
    }

    /// <summary>
    /// Encodes a <see cref="RevocationRequest"/> as <see cref="FormUrlEncodedContent"/>.
    /// </summary>
    internal static FormUrlEncodedContent EncodeRevocation(RevocationRequest request)
    {
        Dictionary<string, string> parameters = ToParameters(request);
        return new FormUrlEncodedContent(parameters);
    }

    /// <summary>
    /// Encodes a <see cref="PushedAuthorizationRequest"/> as <see cref="FormUrlEncodedContent"/>.
    /// </summary>
    internal static FormUrlEncodedContent EncodePar(PushedAuthorizationRequest request)
    {
        Dictionary<string, string> parameters = ToParameters(request);
        return new FormUrlEncodedContent(parameters);
    }

    /// <summary>
    /// Converts a <see cref="TokenRequest"/> to a mutable parameter dictionary.
    /// Callers can apply client authentication before encoding.
    /// </summary>
    internal static Dictionary<string, string> ToParameters(TokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new Dictionary<string, string>
        {
            [OidcConstants.Parameters.ClientId] = request.ClientId,
        };

        switch (request)
        {
            case AuthorizationCodeTokenRequest authCode:
                parameters[OidcConstants.Parameters.GrantType] = OidcConstants.GrantTypes.AuthorizationCode;
                parameters[OidcConstants.Parameters.Code] = authCode.Code;
                parameters[OidcConstants.Parameters.RedirectUri] = authCode.RedirectUri;
                parameters[OidcConstants.Parameters.CodeVerifier] = authCode.CodeVerifier;
                break;

            case RefreshTokenRequest refresh:
                parameters[OidcConstants.Parameters.GrantType] = OidcConstants.GrantTypes.RefreshToken;
                parameters[OidcConstants.Parameters.RefreshToken] = refresh.RefreshToken;
                if (refresh.Scope is not null)
                {
                    parameters[OidcConstants.Parameters.Scope] = refresh.Scope;
                }

                break;

            case ClientCredentialsTokenRequest clientCreds:
                parameters[OidcConstants.Parameters.GrantType] = OidcConstants.GrantTypes.ClientCredentials;
                if (clientCreds.Scope is not null)
                {
                    parameters[OidcConstants.Parameters.Scope] = clientCreds.Scope;
                }

                break;

            case TokenExchangeTokenRequest exchange:
                parameters[OidcConstants.Parameters.GrantType] = OidcConstants.GrantTypes.TokenExchange;
                parameters[OidcConstants.Parameters.SubjectToken] = exchange.SubjectToken;
                parameters[OidcConstants.Parameters.SubjectTokenType] = exchange.SubjectTokenType;
                parameters[OidcConstants.Parameters.Audience] = exchange.Audience;
                if (exchange.Scope is not null)
                {
                    parameters[OidcConstants.Parameters.Scope] = exchange.Scope;
                }

                if (exchange.Resource is not null)
                {
                    parameters[OidcConstants.Parameters.Resource] = exchange.Resource;
                }

                if (exchange.RequestedTokenType is not null)
                {
                    parameters[OidcConstants.Parameters.RequestedTokenType] = exchange.RequestedTokenType;
                }

                if (exchange.ActorToken is not null)
                {
                    parameters[OidcConstants.Parameters.ActorToken] = exchange.ActorToken;
                }

                if (exchange.ActorTokenType is not null)
                {
                    parameters[OidcConstants.Parameters.ActorTokenType] = exchange.ActorTokenType;
                }

                break;
        }

        foreach (KeyValuePair<string, string> kvp in request.AdditionalParameters)
        {
            parameters.TryAdd(kvp.Key, kvp.Value);
        }

        return parameters;
    }

    /// <summary>
    /// Converts a <see cref="RevocationRequest"/> to a mutable parameter dictionary.
    /// </summary>
    internal static Dictionary<string, string> ToParameters(RevocationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Dictionary<string, string> parameters = new()
        {
            [OidcConstants.Parameters.ClientId] = request.ClientId,
            [OidcConstants.Parameters.Token] = request.Token,
        };

        if (request.TokenTypeHint is not null)
        {
            parameters[OidcConstants.Parameters.TokenTypeHint] = request.TokenTypeHint;
        }

        foreach (KeyValuePair<string, string> kvp in request.AdditionalParameters)
        {
            parameters.TryAdd(kvp.Key, kvp.Value);
        }

        return parameters;
    }

    /// <summary>
    /// Converts a <see cref="PushedAuthorizationRequest"/> to a mutable parameter dictionary.
    /// </summary>
    internal static Dictionary<string, string> ToParameters(PushedAuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Dictionary<string, string> parameters = new()
        {
            [OidcConstants.Parameters.ClientId] = request.ClientId,
            [OidcConstants.Parameters.RedirectUri] = request.RedirectUri,
            [OidcConstants.Parameters.ResponseType] = request.ResponseType,
            [OidcConstants.Parameters.Scope] = request.Scope,
            [OidcConstants.Parameters.State] = request.State,
        };

        if (request.CodeChallenge is not null)
        {
            parameters[OidcConstants.Parameters.CodeChallenge] = request.CodeChallenge;
        }

        if (request.CodeChallengeMethod is not null)
        {
            parameters[OidcConstants.Parameters.CodeChallengeMethod] = request.CodeChallengeMethod;
        }

        if (request.Nonce is not null)
        {
            parameters[OidcConstants.Parameters.Nonce] = request.Nonce;
        }

        foreach (KeyValuePair<string, string> kvp in request.AdditionalParameters)
        {
            parameters.TryAdd(kvp.Key, kvp.Value);
        }

        return parameters;
    }
}
