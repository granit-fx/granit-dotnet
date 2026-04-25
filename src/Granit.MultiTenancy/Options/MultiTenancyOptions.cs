namespace Granit.MultiTenancy.Options;

/// <summary>
/// Controls how the <c>X-Tenant-Id</c> header is trusted relative to the JWT claim.
/// </summary>
public enum TenantHeaderTrustMode
{
    /// <summary>
    /// When the user is authenticated and has a <c>tenant_id</c> JWT claim,
    /// the resolved tenant must match the claim. Mismatches return 403 Forbidden.
    /// Prevents spoofing via <c>X-Tenant-Id</c> header tampering. Default since
    /// the framework's secure-by-default tenant-isolation hardening.
    /// </summary>
    CrossValidate,

    /// <summary>
    /// Header is accepted without cross-validation. Only safe behind a fully
    /// trusted reverse proxy (BFF) that scrubs the header from external traffic
    /// and re-emits it from the authenticated session.
    /// </summary>
    Unrestricted,
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
    /// Default: <see cref="TenantHeaderTrustMode.CrossValidate"/> — secure-by-default.
    /// Authenticated callers cannot pivot tenants via the <c>X-Tenant-Id</c> header.
    /// Set to <see cref="TenantHeaderTrustMode.Unrestricted"/> only for deployments
    /// that fully trust their reverse proxy to scrub and re-emit the header.
    /// </summary>
    public TenantHeaderTrustMode HeaderTrustMode { get; set; } = TenantHeaderTrustMode.CrossValidate;

    /// <summary>
    /// Domain template for subdomain-based tenant resolution.
    /// Use <c>{0}</c> as placeholder for the tenant identifier.
    /// Example: <c>"{0}.monsaas.com"</c>. <c>null</c> = disabled.
    /// </summary>
    public string? DomainTemplate { get; set; }

    /// <summary>
    /// When <c>true</c>, the middleware verifies that the resolved tenant ID
    /// exists in <see cref="Stores.ITenantReader"/> before activating the context.
    /// Prevents phantom tenants (arbitrary GUIDs from spoofed headers) from creating
    /// orphaned data. Default: <c>true</c>.
    /// </summary>
    /// <remarks>
    /// Requires <c>Granit.MultiTenancy.EntityFrameworkCore</c> for the real
    /// <see cref="Stores.ITenantReader"/> implementation. Without it, the
    /// <c>NullTenantReader</c> returns <c>true</c> for all IDs — validation
    /// is effectively skipped (safe degradation to avoid denial of service).
    /// </remarks>
    public bool ValidateTenantExistence { get; set; } = true;

    /// <summary>
    /// Query string parameter name for tenant resolution (dev/debug only).
    /// Default: <c>null</c> (disabled). Enable in <c>appsettings.Development.json</c>
    /// by setting to <c>"__tenant"</c>. Never enable in production — allows
    /// unauthenticated callers to select an arbitrary tenant context.
    /// </summary>
    public string? QueryStringParamName { get; set; }

    /// <summary>
    /// Strategy for generating outbound tenant-specific URLs (email links, templates, etc.).
    /// Default: <see cref="TenantUrlStrategy.Shared"/> (backward-compatible, static URL).
    /// </summary>
    public TenantUrlStrategy UrlStrategy { get; set; } = TenantUrlStrategy.Shared;

    /// <summary>
    /// URI scheme for generated tenant URLs.
    /// Default: <c>"https"</c>. Use <c>"http"</c> only in development.
    /// </summary>
    public string UrlScheme { get; set; } = "https";

    /// <summary>
    /// Static fallback base URL used when <see cref="UrlStrategy"/> is
    /// <see cref="TenantUrlStrategy.Shared"/> or when no tenant context is active.
    /// Typically matches <c>Granit:Templating:App:BaseUrl</c>.
    /// No trailing slash.
    /// </summary>
    public string? FallbackBaseUrl { get; set; }
}
