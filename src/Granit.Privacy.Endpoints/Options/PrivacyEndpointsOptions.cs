namespace Granit.Privacy.Endpoints.Options;

/// <summary>
/// Configuration options for the privacy GDPR endpoints.
/// </summary>
public sealed class PrivacyEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Privacy:Endpoints";

    /// <summary>
    /// Route prefix for all privacy endpoints.
    /// Default: <c>"privacy"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "privacy";

    /// <summary>
    /// OpenAPI tag name for grouping privacy endpoints.
    /// Default: <c>"Privacy"</c>.
    /// </summary>
    public string TagName { get; set; } = "Privacy";

    /// <summary>
    /// Rate limiting policy name applied to all Privacy endpoints.
    /// When <c>null</c> (default), no rate limiting is applied.
    /// Set to a policy name registered via <c>AddRateLimiter()</c> to protect against
    /// resource exhaustion through excessive export/deletion requests (OWASP API4).
    /// </summary>
    public string? RateLimitingPolicy { get; set; }

    /// <summary>
    /// Whether the privacy-export download endpoints require a recent re-authentication
    /// (step-up auth). When <see langword="true"/> (default), the OIDC <c>auth_time</c>
    /// claim must be within <see cref="DownloadStepUpMaxAge"/> of the current time;
    /// otherwise the endpoint returns <c>401</c> with a <c>WWW-Authenticate: Bearer
    /// error="step_up"</c> header so an OIDC-aware BFF can refresh the session
    /// transparently before retrying.
    /// </summary>
    /// <remarks>
    /// A long-lived session cookie is not sufficient evidence to release a GDPR
    /// archive — a stolen cookie alone must not let an attacker pivot to a full
    /// personal-data export. Hosts running entirely behind a hardware token wall
    /// can flip this off; the default is on.
    /// </remarks>
    public bool DownloadStepUpRequired { get; set; } = true;

    /// <summary>
    /// Maximum age of the OIDC <c>auth_time</c> claim for the privacy-export download
    /// endpoints. Defaults to 5 minutes.
    /// </summary>
    public TimeSpan DownloadStepUpMaxAge { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether the anonymous <c>POST /privacy/opt-out</c> request honours the resolved
    /// <c>ICurrentTenant</c> when writing the opt-out record. Defaults to
    /// <see langword="false"/> — anonymous opt-out records are stored tenant-less to
    /// prevent an unauthenticated caller from selecting an arbitrary tenant via a
    /// spoofable resolver (e.g. <c>X-Tenant-Id</c> header). Hosts running with a
    /// non-spoofable resolver (subdomain, mTLS SAN) may opt back in.
    /// </summary>
    /// <remarks>
    /// Even when disabled, the visitor's authenticated re-issue of the opt-out
    /// rebinds the record to their own tenant.
    /// </remarks>
    public bool BindAnonymousOptOutToCurrentTenant { get; set; }
}
