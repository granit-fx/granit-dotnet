using Granit.Events;

namespace Granit.Identity.Federated.Events;

/// <summary>
/// Audit event published every time the federated identity layer exchanges service-account
/// credentials for a user-scoped access token (RFC 8693 — direct naked impersonation).
/// </summary>
/// <remarks>
/// <para>
/// Token exchange grants a service the ability to act as any registered user without their
/// consent. ISO 27001 A.12.4 (logging and monitoring of privileged operations) requires
/// every such operation to leave an immutable audit trail in the consuming application's
/// SIEM. This event is the propagation channel — subscribe to it from
/// <see cref="IDistributedEventBus"/> consumers (Wolverine handlers, audit pipelines)
/// to persist the trail.
/// </para>
/// <para>
/// The event carries no PII — only the target user identifier, the operation reason,
/// and the timestamp.
/// </para>
/// </remarks>
/// <param name="TargetUserId">The user identifier the new token impersonates.</param>
/// <param name="Reason">Free-text reason / operation name (e.g. <c>"device-activity"</c>).</param>
/// <param name="OccurredAt">UTC timestamp of the token exchange.</param>
public sealed record IdentityTokenExchangedEto(
    string TargetUserId,
    string Reason,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
