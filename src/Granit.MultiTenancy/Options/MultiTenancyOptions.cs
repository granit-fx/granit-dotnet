namespace Granit.MultiTenancy.Options;

/// <summary>
/// Controls how the <c>X-Tenant-Id</c> header is trusted relative to the JWT claim.
/// </summary>
public enum TenantHeaderTrustMode
{
    /// <summary>
    /// Header is accepted without cross-validation (default, backward-compatible).
    /// Use behind a BFF or reverse proxy that sets the header from authenticated context.
    /// </summary>
    Unrestricted,

    /// <summary>
    /// When the user is authenticated and has a <c>tenant_id</c> JWT claim,
    /// the resolved tenant must match the claim. Mismatches return 403 Forbidden.
    /// Prevents spoofing via <c>X-Tenant-Id</c> header tampering.
    /// </summary>
    CrossValidate,
}

/// <summary>
/// Configuration options for the MultiTenancy module.
/// </summary>
public sealed class MultiTenancyOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "MultiTenancy";

    /// <summary>
    /// Enables or disables tenant resolution by the middleware.
    /// Disable in single-tenant environments or for tests.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// JWT claim type containing the tenant identifier.
    /// Default value: "tenant_id" (standard Keycloak claim).
    /// </summary>
    public string TenantIdClaimType { get; set; } = "tenant_id";

    /// <summary>
    /// HTTP header name containing the tenant identifier.
    /// Default value: "X-Tenant-Id".
    /// </summary>
    public string TenantIdHeaderName { get; set; } = "X-Tenant-Id";

    /// <summary>
    /// Controls how the tenant header is validated against JWT claims.
    /// Default: <see cref="TenantHeaderTrustMode.Unrestricted"/> (backward-compatible).
    /// Set to <see cref="TenantHeaderTrustMode.CrossValidate"/> for environments where
    /// the header may be attacker-controlled (no trusted reverse proxy).
    /// </summary>
    public TenantHeaderTrustMode HeaderTrustMode { get; set; } = TenantHeaderTrustMode.Unrestricted;
}
