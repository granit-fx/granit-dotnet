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
}
