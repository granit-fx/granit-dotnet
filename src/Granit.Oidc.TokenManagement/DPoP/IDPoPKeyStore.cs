namespace Granit.Oidc.TokenManagement.DPoP;

/// <summary>
/// Provides a stable DPoP (RFC 9449) proof key per named HTTP client.
/// </summary>
/// <remarks>
/// DPoP-bound access tokens carry a <c>cnf.jkt</c> claim pinned to the thumbprint of
/// the key that requested them. Delegating handlers are recreated every
/// <c>HttpClientFactory</c> handler-lifetime rotation (~2&#160;min by default), whereas a
/// token is cached for its full lifetime — often much longer. If the proof key lived on
/// the handler instance it would change on every rotation, leaving cached tokens bound to
/// a thumbprint the new key no longer matches (<c>invalid_dpop_proof</c>). Holding the key
/// in a singleton keyed by client name keeps it stable for the process lifetime so cached
/// tokens stay valid for their whole TTL.
/// </remarks>
internal interface IDPoPKeyStore
{
    /// <summary>
    /// Returns the stable DPoP private-key JWK for <paramref name="clientName"/>,
    /// generating one on first use.
    /// </summary>
    string GetOrCreateKey(string clientName);
}
