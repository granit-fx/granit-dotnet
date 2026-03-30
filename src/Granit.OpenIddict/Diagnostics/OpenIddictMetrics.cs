using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.OpenIddict.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the OpenIddict OIDC protocol module.
/// Meter: <c>Granit.OpenIddict</c>.
/// </summary>
/// <remarks>
/// <para>
/// All metrics follow the <c>granit.openiddict.{entity}.{action}</c> naming convention
/// and include <c>tenant_id</c> (coalesced to <c>"global"</c>) via <see cref="TagList"/>.
/// </para>
/// <para>
/// Account-level metrics (login, registration, password, 2FA, impersonation, external logins)
/// have moved to <c>Granit.Identity.Local.Diagnostics.IdentityLocalMetrics</c>.
/// </para>
/// </remarks>
#pragma warning disable GRSEC003 // Metric name constants, not secrets
public sealed class OpenIddictMetrics(IMeterFactory meterFactory)
{
    /// <summary>The meter name for this module.</summary>
    public const string MeterName = "Granit.OpenIddict";

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenantId = "global";
    private const string UnknownTagValue = "unknown";

    private static readonly HashSet<string> AllowedGrantTypes =
    [
        "authorization_code", "client_credentials", "refresh_token",
        "urn:ietf:params:oauth:grant-type:device_code",
        "urn:ietf:params:oauth:grant-type:token-exchange",
        "urn:granit:grant_type:two_factor",
        "urn:granit:grant_type:passkey",
    ];

    private static readonly HashSet<string> AllowedAuthFailureReasons =
    [
        "invalid_credentials", "account_locked", "email_not_confirmed",
        "two_factor_required", "invalid_token", "expired_token",
    ];

    private static string SanitizeGrantType(string grantType) =>
        AllowedGrantTypes.Contains(grantType) ? grantType : UnknownTagValue;

    private static string SanitizeReason(string reason) =>
        AllowedAuthFailureReasons.Contains(reason) ? reason : UnknownTagValue;

    private readonly Counter<long> _tokensIssued = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.tokens.issued",
        description: "Number of tokens issued.");

    private readonly Counter<long> _tokensRevoked = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.tokens.revoked",
        description: "Number of tokens revoked.");

    private readonly Counter<long> _authenticationSuccesses = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.authentication.successes",
        description: "Number of successful OIDC grant-type authentications.");

    private readonly Counter<long> _authenticationFailures = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.authentication.failures",
        description: "Number of failed OIDC grant-type authentication attempts.");

    private readonly Counter<long> _keyRotations = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.keys.rotated",
        description: "Number of key rotation cycles.");

    private readonly Histogram<double> _tokenIssuanceDuration = meterFactory.Create(MeterName).CreateHistogram<double>(
        "granit.openiddict.token.issuance.duration",
        unit: "s", description: "Duration of token issuance in seconds.");

    /// <summary>Records a token issuance.</summary>
    public void RecordTokenIssued(string? tenantId, string grantType) =>
        _tokensIssued.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "grant_type", SanitizeGrantType(grantType) } });

    /// <summary>Records a token revocation.</summary>
    public void RecordTokenRevoked(string? tenantId, string reason) =>
        _tokensRevoked.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "reason", SanitizeReason(reason) } });

    /// <summary>Records a successful OIDC grant-type authentication.</summary>
    public void RecordAuthenticationSuccess(string? tenantId, string grantType) =>
        _authenticationSuccesses.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "grant_type", SanitizeGrantType(grantType) } });

    /// <summary>Records a failed OIDC grant-type authentication attempt.</summary>
    public void RecordAuthenticationFailure(string? tenantId, string reason) =>
        _authenticationFailures.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "reason", SanitizeReason(reason) } });

    /// <summary>Records a key rotation cycle.</summary>
    public void RecordKeyRotation(string? tenantId, int keysGenerated, int keysRetired, int keysRevoked) =>
        _keyRotations.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "keys_generated", keysGenerated }, { "keys_retired", keysRetired }, { "keys_revoked", keysRevoked } });

    /// <summary>Records the duration of a token issuance.</summary>
    public void RecordTokenIssuanceDuration(string? tenantId, string grantType, TimeSpan duration) =>
        _tokenIssuanceDuration.Record(duration.TotalSeconds, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "grant_type", SanitizeGrantType(grantType) } });
}
#pragma warning restore GRSEC003
