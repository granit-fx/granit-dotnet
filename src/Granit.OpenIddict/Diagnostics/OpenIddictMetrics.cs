using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.OpenIddict.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the OpenIddict module.
/// Meter: <c>Granit.OpenIddict</c>.
/// </summary>
/// <remarks>
/// All metrics follow the <c>granit.openiddict.{entity}.{action}</c> naming convention
/// and include <c>tenant_id</c> (coalesced to <c>"global"</c>) via <see cref="TagList"/>.
/// </remarks>
#pragma warning disable GRSEC003 // Metric name constants, not secrets
public sealed class OpenIddictMetrics(IMeterFactory meterFactory)
{
    /// <summary>The meter name for this module.</summary>
    public const string MeterName = "Granit.OpenIddict";

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenantId = "global";

    private readonly Counter<long> _tokensIssued = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.tokens.issued",
        description: "Number of tokens issued.");

    private readonly Counter<long> _tokensRevoked = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.tokens.revoked",
        description: "Number of tokens revoked.");

    private readonly Counter<long> _authenticationSuccesses = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.authentication.successes",
        description: "Number of successful authentications.");

    private readonly Counter<long> _authenticationFailures = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.authentication.failures",
        description: "Number of failed authentication attempts.");

    private readonly Counter<long> _registrations = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.users.registered",
        description: "Number of user registrations.");

    private readonly Counter<long> _passwordChanges = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.password.changes",
        description: "Number of password changes.");

    private readonly Counter<long> _passwordResets = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.password.resets",
        description: "Number of password resets.");

    private readonly Counter<long> _accountDeletions = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.account.deletions",
        description: "Number of account deletions (GDPR).");

    private readonly Counter<long> _impersonations = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.users.impersonated",
        description: "Number of user impersonations.");

    private readonly Counter<long> _twoFactorEvents = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.twofactor.events",
        description: "Number of 2FA events (enable, disable, verify).");

    private readonly Counter<long> _externalLogins = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.external.logins",
        description: "Number of external login events.");

    private readonly Counter<long> _keyRotations = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.openiddict.keys.rotated",
        description: "Number of key rotation cycles.");

    private readonly Histogram<double> _tokenIssuanceDuration = meterFactory.Create(MeterName).CreateHistogram<double>(
        "granit.openiddict.token.issuance.duration",
        unit: "s", description: "Duration of token issuance in seconds.");

    /// <summary>Records a token issuance.</summary>
    public void RecordTokenIssued(string? tenantId, string grantType) =>
        _tokensIssued.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "grant_type", grantType } });

    /// <summary>Records a token revocation.</summary>
    public void RecordTokenRevoked(string? tenantId, string reason) =>
        _tokensRevoked.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "reason", reason } });

    /// <summary>Records a successful authentication.</summary>
    public void RecordAuthenticationSuccess(string? tenantId, string grantType) =>
        _authenticationSuccesses.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "grant_type", grantType } });

    /// <summary>Records a failed authentication attempt.</summary>
    public void RecordAuthenticationFailure(string? tenantId, string reason) =>
        _authenticationFailures.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "reason", reason } });

    /// <summary>Records a user registration.</summary>
    public void RecordRegistration(string? tenantId) =>
        _registrations.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId } });

    /// <summary>Records a password change.</summary>
    public void RecordPasswordChange(string? tenantId) =>
        _passwordChanges.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId } });

    /// <summary>Records a password reset (forgot password flow).</summary>
    public void RecordPasswordReset(string? tenantId) =>
        _passwordResets.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId } });

    /// <summary>Records an account deletion (GDPR).</summary>
    public void RecordAccountDeletion(string? tenantId) =>
        _accountDeletions.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId } });

    /// <summary>Records an impersonation event.</summary>
    public void RecordImpersonation(string? tenantId) =>
        _impersonations.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId } });

    /// <summary>Records a 2FA event (enable, disable, verify).</summary>
    public void RecordTwoFactorEvent(string? tenantId, string action) =>
        _twoFactorEvents.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "action", action } });

    /// <summary>Records an external login event.</summary>
    public void RecordExternalLogin(string? tenantId, string provider, bool isNewUser) =>
        _externalLogins.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "provider", provider }, { "is_new_user", isNewUser } });

    /// <summary>Records a key rotation cycle.</summary>
    public void RecordKeyRotation(string? tenantId, int keysGenerated, int keysRetired, int keysRevoked) =>
        _keyRotations.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "keys_generated", keysGenerated }, { "keys_retired", keysRetired }, { "keys_revoked", keysRevoked } });

    /// <summary>Records the duration of a token issuance.</summary>
    public void RecordTokenIssuanceDuration(string? tenantId, string grantType, TimeSpan duration) =>
        _tokenIssuanceDuration.Record(duration.TotalSeconds, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "grant_type", grantType } });
}
#pragma warning restore GRSEC003
