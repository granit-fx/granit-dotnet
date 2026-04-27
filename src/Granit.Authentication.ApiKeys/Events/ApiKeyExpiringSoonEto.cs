using Granit.Authentication.ApiKeys.Domain;
using Granit.Events;

namespace Granit.Authentication.ApiKeys.Events;

/// <summary>
/// Integration event emitted by the daily scanner when an API key is approaching
/// expiration (within the lead time configured via
/// <see cref="Options.ApiKeysOptions.ExpirationLeadTimeDays"/>). Drives the proactive
/// "expiring soon" notification routed by <c>Granit.Authentication.ApiKeys.Notifications</c>
/// to tenant administrators so rotation can be scheduled — supporting ISO 27001 A.9.4
/// (least-privilege rotation policy) and avoiding service disruption from silent
/// expiration.
/// </summary>
/// <remarks>
/// <para>
/// <b>Secret hygiene — defence in depth.</b> This Eto deliberately carries only
/// public-safe metadata: <see cref="KeyId"/>, <see cref="KeyName"/>,
/// <see cref="KeyType"/>, <see cref="ExpiresAt"/>, <see cref="TenantId"/>. It NEVER
/// carries the SHA-256 hash, the raw key value, or even the key prefix. The prefix is
/// excluded on purpose: this notification's only purpose is to nudge an administrator
/// to rotate by name, not to identify the key by its on-the-wire shape. Downstream
/// consumers that need more detail look up <see cref="KeyId"/> against
/// <c>IApiKeyAdminStore</c>.
/// </para>
/// <para>
/// <b>Dedupe.</b> The scanner stamps <c>ApiKeyEntry.LastExpirationNotifiedAt</c> on
/// emission and skips keys notified within the last week, so admins are not flooded
/// when a key sits in the lead-time window for several days.
/// </para>
/// </remarks>
/// <param name="KeyId">Stable identifier of the expiring API key.</param>
/// <param name="KeyName">Human-readable display name (e.g. <c>Partner Lab X</c>).</param>
/// <param name="KeyType">Key category (Secret, Publishable, Webhook, Ephemeral).</param>
/// <param name="ExpiresAt">UTC instant at which the key stops being accepted.</param>
/// <param name="TenantId">Owning tenant; <c>null</c> for global (host-level) keys.</param>
public sealed record ApiKeyExpiringSoonEto(
    Guid KeyId,
    string KeyName,
    ApiKeyType KeyType,
    DateTimeOffset ExpiresAt,
    Guid? TenantId = null) : IIntegrationEvent;
