namespace Granit.OpenIddict.Options;

/// <summary>
/// Static configuration options for the Granit OpenIddict module.
/// </summary>
/// <remarks>
/// Dynamic per-tenant values (token lifetimes, max login attempts) are configured via
/// <c>Granit.Settings</c> using <see cref="OpenIddictSettingNames"/> constants.
/// </remarks>
public sealed class GranitOpenIddictOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "OpenIddict";

    /// <summary>
    /// Gets or sets the OIDC issuer URI.
    /// </summary>
    /// <remarks>
    /// When <see langword="null"/>, the issuer is inferred from the request URL.
    /// Set explicitly for multi-node deployments behind a load balancer.
    /// </remarks>
    public Uri? Issuer { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether OpenIddict entity caching is enabled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Defaults to <see langword="false"/> because OpenIddict's entity cache uses <c>ClientId</c>
    /// as the sole cache key, which causes cross-tenant pollution in multi-tenant setups
    /// (two tenants with the same <c>ClientId</c> would share cached data).
    /// </para>
    /// <para>
    /// Enable only for single-tenant deployments where cache performance is critical.
    /// </para>
    /// </remarks>
    public bool EnableEntityCaching { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether OpenIddict should issue opaque reference tokens
    /// instead of self-contained JWTs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference tokens are validated against the database on every API request, enabling
    /// instant revocation. Required for banking, healthcare, and regulated industries
    /// (SOC2 / ISO 27001 strict).
    /// </para>
    /// <para>
    /// Trade-off: one DB round-trip per API call. Mitigate with a Redis token store
    /// for high-traffic scenarios.
    /// </para>
    /// <para>
    /// Default: <see langword="false"/> (JWT stateless — best performance for most use cases).
    /// </para>
    /// </remarks>
    public bool UseReferenceTokens { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the server requires Pushed Authorization Requests
    /// (RFC 9126) for all authorization code flows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The PAR endpoint (<c>/connect/par</c>) is always registered. When this option is
    /// <see langword="true"/>, the server rejects direct authorization requests that do not
    /// include a <c>request_uri</c> obtained from the PAR endpoint.
    /// </para>
    /// <para>
    /// Required for FAPI 2.0 Security Profile compliance.
    /// Default: <see langword="false"/> (PAR available but not enforced).
    /// </para>
    /// </remarks>
    public bool RequirePar { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether OAuth 2.0 Token Exchange (RFC 8693) is enabled.
    /// Allows services to exchange an access token for a more constrained, audience-restricted one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Token exchange enables delegation and impersonation flows in microservice architectures.
    /// A service receiving a user's token can exchange it for a narrower token scoped to a
    /// downstream service's audience.
    /// </para>
    /// <para>Default: <see langword="false"/>.</para>
    /// </remarks>
    public bool EnableTokenExchange { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the FAPI 2.0 Security Profile is enabled.
    /// When <see langword="true"/>, all FAPI 2.0 mandatory constraints are enforced.
    /// </summary>
    /// <remarks>
    /// <para>Enabling this profile automatically sets:</para>
    /// <list type="bullet">
    /// <item><description><see cref="RequirePar"/> = <c>true</c> (RFC 9126)</description></item>
    /// </list>
    /// <para>
    /// Additionally, the following must be configured on the BFF side:
    /// <c>UseDPoP</c>, <c>UsePushedAuthorizationRequests</c>,
    /// <c>ClientAuthenticationMethod = PrivateKeyJwt</c>.
    /// </para>
    /// <para>Default: <see langword="false"/>.</para>
    /// </remarks>
    public bool EnableFapi2Profile { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the OpenIddict server is allowed to start with
    /// ephemeral signing and encryption keys (regenerated at every process start).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ephemeral keys invalidate every issued token on restart and produce inconsistent
    /// signing material across instances of a multi-replica deployment — symptoms range
    /// from mass session loss to intermittent token validation failures.
    /// </para>
    /// <para>
    /// Default: <see langword="false"/>. The framework refuses to start in non-Development
    /// environments unless persistent signing/encryption material is configured.
    /// Set to <see langword="true"/> only for tests or single-instance dev loops.
    /// </para>
    /// </remarks>
    public bool AllowEphemeralKeys { get; set; }
}

/// <summary>
/// Extension methods for <see cref="GranitOpenIddictOptions"/> FAPI 2.0 profile.
/// </summary>
public static class GranitOpenIddictOptionsExtensions
{
    /// <summary>
    /// Enables the FAPI 2.0 Security Profile on the OpenIddict server.
    /// Enforces PAR, PKCE S256, DPoP sender-constraining, private_key_jwt,
    /// PS256 signing algorithm, and issuer verification — all mandatory FAPI 2.0 requirements.
    /// </summary>
    /// <param name="options">The OpenIddict options.</param>
    /// <returns>The options for chaining.</returns>
    public static GranitOpenIddictOptions WithFapi2Profile(this GranitOpenIddictOptions options)
    {
        options.EnableFapi2Profile = true;
        options.RequirePar = true;
        options.UseReferenceTokens = true;
        return options;
    }
}
