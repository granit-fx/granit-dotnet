using Granit.Events;

namespace Granit.Identity.Federated.Events;

/// <summary>
/// Integration event raised when the federated identity layer fails to synchronise
/// a user between the upstream identity provider (Keycloak, Entra ID, Cognito,
/// Google Cloud, …) and the local <c>UserCacheEntry</c> table. ISO 27001 A.12.4
/// requires drift between the IdP and our user cache to surface to a human
/// responder rather than only land in a log file.
/// </summary>
/// <remarks>
/// <para>
/// The Eto is rate-limited at the emission site to one occurrence per
/// (<paramref name="UserId"/>, <paramref name="ProviderName"/>) per cool-off
/// window (default 60 minutes — see
/// <c>IdentityFederatedNotificationOptions.SyncFailureCoolOffMinutes</c>) so a
/// repeatedly-failing user does not flood the SIEM / notification channel.
/// The accompanying <c>LogUserNotFoundInProvider</c>-class log lines are
/// preserved unchanged so existing log-based alerting keeps working during the
/// rollout.
/// </para>
/// <para>
/// SOC integrations can subscribe directly to this Eto via Wolverine
/// (e.g. for SIEM routing); platform administrators receive the
/// <c>identity.sync_failed</c> notification through
/// <c>Granit.Identity.Federated.Notifications</c>.
/// </para>
/// </remarks>
/// <param name="UserId">External user identifier in the identity provider that failed to sync.</param>
/// <param name="ProviderName">Logical provider name (<c>"Keycloak"</c>, <c>"EntraId"</c>, <c>"Cognito"</c>, <c>"GoogleCloud"</c>, …).</param>
/// <param name="Reason">Short, human-readable reason. No PII; safe to surface in notifications and SIEM dashboards.</param>
/// <param name="OccurredAt">UTC timestamp of the failure (sourced from <see cref="TimeProvider"/>).</param>
/// <param name="TenantId">Optional tenant scope of the failed sync. <c>null</c> when the operation is not bound to a single tenant.</param>
public sealed record IdentityUserSyncFailedEto(
    string UserId,
    string ProviderName,
    string Reason,
    DateTimeOffset OccurredAt,
    Guid? TenantId = null) : IIntegrationEvent;
