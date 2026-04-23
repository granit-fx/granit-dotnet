#pragma warning disable GRSEC003 // Constants class defines OAuth 2.0 protocol parameter names — not actual secrets

namespace Granit.Oidc;

/// <summary>
/// Standard OIDC/OAuth 2.0 constants for parameter names, grant types, and discovery metadata fields.
/// </summary>
public static class OidcConstants
{
    /// <summary>
    /// OAuth 2.0 grant type identifiers.
    /// </summary>
    public static class GrantTypes
    {
        /// <summary>Authorization code grant (RFC 6749 §4.1).</summary>
        public const string AuthorizationCode = "authorization_code";

        /// <summary>Refresh token grant (RFC 6749 §6).</summary>
        public const string RefreshToken = "refresh_token";

        /// <summary>Client credentials grant (RFC 6749 §4.4).</summary>
        public const string ClientCredentials = "client_credentials";

        /// <summary>Token exchange grant (RFC 8693).</summary>
        public const string TokenExchange = "urn:ietf:params:oauth:grant-type:token-exchange";

        /// <summary>Device authorization grant (RFC 8628).</summary>
        public const string DeviceCode = "urn:ietf:params:oauth:grant-type:device_code";
    }

    /// <summary>
    /// OAuth 2.0 response type values.
    /// </summary>
    public static class ResponseTypes
    {
        /// <summary>Authorization code response type.</summary>
        public const string Code = "code";
    }

    /// <summary>
    /// PKCE code challenge methods (RFC 7636).
    /// </summary>
    public static class CodeChallengeMethods
    {
        /// <summary>SHA-256 code challenge method.</summary>
        public const string S256 = "S256";
    }

    /// <summary>
    /// OAuth 2.0 token type hint values for revocation (RFC 7009).
    /// </summary>
    public static class TokenTypes
    {
        /// <summary>Access token type hint.</summary>
        public const string AccessToken = "access_token";

        /// <summary>Refresh token type hint.</summary>
        public const string RefreshToken = "refresh_token";
    }

    /// <summary>
    /// OAuth 2.0 token type identifiers (RFC 8693 §3).
    /// </summary>
    public static class TokenTypeIdentifiers
    {
        /// <summary>Access token type identifier for token exchange.</summary>
        public const string AccessToken = "urn:ietf:params:oauth:token-type:access_token";

        /// <summary>Refresh token type identifier for token exchange.</summary>
        public const string RefreshToken = "urn:ietf:params:oauth:token-type:refresh_token";
    }

    /// <summary>
    /// Client assertion type identifiers (RFC 7523).
    /// </summary>
    public static class ClientAssertionTypes
    {
        /// <summary>JWT Bearer client assertion type.</summary>
        public const string JwtBearer = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
    }

    /// <summary>
    /// Standard OAuth 2.0 and OIDC request/response parameter names.
    /// </summary>
    public static class Parameters
    {
        /// <summary>Client identifier parameter.</summary>
        public const string ClientId = "client_id";

        /// <summary>Client secret parameter.</summary>
        public const string ClientSecret = "client_secret";

        /// <summary>Grant type parameter.</summary>
        public const string GrantType = "grant_type";

        /// <summary>Authorization code parameter.</summary>
        public const string Code = "code";

        /// <summary>Redirect URI parameter.</summary>
        public const string RedirectUri = "redirect_uri";

        /// <summary>PKCE code verifier parameter.</summary>
        public const string CodeVerifier = "code_verifier";

        /// <summary>PKCE code challenge parameter.</summary>
        public const string CodeChallenge = "code_challenge";

        /// <summary>PKCE code challenge method parameter.</summary>
        public const string CodeChallengeMethod = "code_challenge_method";

        /// <summary>Scope parameter.</summary>
        public const string Scope = "scope";

        /// <summary>State parameter for CSRF protection.</summary>
        public const string State = "state";

        /// <summary>Response type parameter.</summary>
        public const string ResponseType = "response_type";

        /// <summary>Refresh token parameter.</summary>
        public const string RefreshToken = "refresh_token";

        /// <summary>Token parameter (revocation endpoint).</summary>
        public const string Token = "token";

        /// <summary>Token type hint parameter (revocation endpoint).</summary>
        public const string TokenTypeHint = "token_type_hint";

        /// <summary>Client assertion parameter (RFC 7523).</summary>
        public const string ClientAssertion = "client_assertion";

        /// <summary>Client assertion type parameter (RFC 7523).</summary>
        public const string ClientAssertionType = "client_assertion_type";

        /// <summary>PAR request URI parameter (RFC 9126).</summary>
        public const string RequestUri = "request_uri";

        /// <summary>Nonce parameter for replay protection.</summary>
        public const string Nonce = "nonce";

        /// <summary>ID token hint parameter for end session.</summary>
        public const string IdTokenHint = "id_token_hint";

        /// <summary>Post-logout redirect URI parameter.</summary>
        public const string PostLogoutRedirectUri = "post_logout_redirect_uri";

        /// <summary>Subject token parameter (RFC 8693 §2.1 — token exchange).</summary>
        public const string SubjectToken = "subject_token";

        /// <summary>Subject token type parameter (RFC 8693 §2.1).</summary>
        public const string SubjectTokenType = "subject_token_type";

        /// <summary>Actor token parameter (RFC 8693 §2.1 — delegation chaining).</summary>
        public const string ActorToken = "actor_token";

        /// <summary>Actor token type parameter (RFC 8693 §2.1).</summary>
        public const string ActorTokenType = "actor_token_type";

        /// <summary>Audience parameter (RFC 8693 §2.1 — restricts the issued token's aud claim).</summary>
        public const string Audience = "audience";

        /// <summary>Resource parameter (RFC 8693 §2.1 / RFC 8707).</summary>
        public const string Resource = "resource";

        /// <summary>Requested token type parameter (RFC 8693 §2.1).</summary>
        public const string RequestedTokenType = "requested_token_type";
    }

    /// <summary>
    /// OIDC discovery document metadata field names (RFC 8414).
    /// </summary>
    public static class Discovery
    {
        /// <summary>Issuer identifier.</summary>
        public const string Issuer = "issuer";

        /// <summary>Authorization endpoint URL.</summary>
        public const string AuthorizationEndpoint = "authorization_endpoint";

        /// <summary>Token endpoint URL.</summary>
        public const string TokenEndpoint = "token_endpoint";

        /// <summary>Token revocation endpoint URL.</summary>
        public const string RevocationEndpoint = "revocation_endpoint";

        /// <summary>End session (logout) endpoint URL.</summary>
        public const string EndSessionEndpoint = "end_session_endpoint";

        /// <summary>JSON Web Key Set URI.</summary>
        public const string JwksUri = "jwks_uri";

        /// <summary>Pushed Authorization Request endpoint URL (RFC 9126).</summary>
        public const string PushedAuthorizationRequestEndpoint = "pushed_authorization_request_endpoint";

        /// <summary>Supported scopes.</summary>
        public const string ScopesSupported = "scopes_supported";

        /// <summary>Supported grant types.</summary>
        public const string GrantTypesSupported = "grant_types_supported";

        /// <summary>Supported response types.</summary>
        public const string ResponseTypesSupported = "response_types_supported";

        /// <summary>Supported DPoP signing algorithms (RFC 9449).</summary>
        public const string DPoPSigningAlgValuesSupported = "dpop_signing_alg_values_supported";
    }

    /// <summary>
    /// JSON Web Signature algorithm identifiers.
    /// </summary>
    public static class Algorithms
    {
        /// <summary>ECDSA using P-256 and SHA-256.</summary>
        public const string ES256 = "ES256";

        /// <summary>RSASSA-PSS using SHA-256 and MGF1 with SHA-256.</summary>
        public const string PS256 = "PS256";
    }
}
