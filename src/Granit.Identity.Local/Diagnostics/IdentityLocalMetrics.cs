using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Identity.Local.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for local identity account operations.
/// Meter: <c>Granit.Identity.Local</c>.
/// </summary>
/// <remarks>
/// All metrics follow the <c>granit.identity.local.{entity}.{action}</c> naming convention
/// and include <c>tenant_id</c> (coalesced to <c>"global"</c>) via <see cref="TagList"/>.
/// </remarks>
#pragma warning disable GRSEC003 // Metric name constants, not secrets
public sealed class IdentityLocalMetrics(IMeterFactory meterFactory)
{
    /// <summary>The meter name for this module.</summary>
    public const string MeterName = "Granit.Identity.Local";

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenantId = "global";
    private const string UnknownTagValue = "unknown";

    private static readonly HashSet<string> AllowedAuthFailureReasons =
    [
        "invalid_credentials", "account_locked", "email_not_confirmed",
        "two_factor_required", "invalid_token", "expired_token",
    ];

    private static readonly HashSet<string> AllowedProviders =
    [
        "Google", "Microsoft", "GitHub", "Apple", "Facebook",
    ];

    private static string SanitizeReason(string reason) =>
        AllowedAuthFailureReasons.Contains(reason) ? reason : UnknownTagValue;

    private static string SanitizeProvider(string provider) =>
        AllowedProviders.Contains(provider) ? provider : UnknownTagValue;

    private readonly Counter<long> _authenticationSuccesses = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.identity.local.authentication.successes",
        description: "Number of successful authentications.");

    private readonly Counter<long> _authenticationFailures = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.identity.local.authentication.failures",
        description: "Number of failed authentication attempts.");

    private readonly Counter<long> _registrations = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.identity.local.users.registered",
        description: "Number of user registrations.");

    private readonly Counter<long> _passwordChanges = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.identity.local.password.changes",
        description: "Number of password changes.");

    private readonly Counter<long> _passwordResets = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.identity.local.password.resets",
        description: "Number of password resets.");

    private readonly Counter<long> _accountDeletions = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.identity.local.account.deletions",
        description: "Number of account deletions (GDPR).");

    private readonly Counter<long> _impersonations = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.identity.local.users.impersonated",
        description: "Number of user impersonations.");

    private readonly Counter<long> _twoFactorEvents = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.identity.local.twofactor.events",
        description: "Number of 2FA events (enable, disable, verify).");

    private readonly Counter<long> _externalLogins = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.identity.local.external.logins",
        description: "Number of external login events.");

    /// <summary>Records a successful authentication.</summary>
    public void RecordAuthenticationSuccess(string? tenantId, string grantType) =>
        _authenticationSuccesses.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "grant_type", grantType } });

    /// <summary>Records a failed authentication attempt.</summary>
    public void RecordAuthenticationFailure(string? tenantId, string reason) =>
        _authenticationFailures.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "reason", SanitizeReason(reason) } });

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
        _externalLogins.Add(1, new TagList { { TenantIdTag, tenantId ?? GlobalTenantId }, { "provider", SanitizeProvider(provider) }, { "is_new_user", isNewUser } });
}
#pragma warning restore GRSEC003
