using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Abstractions;

/// <summary>
/// Result of creating a webhook subscription. Contains the persisted entity and the
/// plain-text signing secret (returned once — never stored in clear text).
/// </summary>
public sealed record WebhookSubscriptionCreatedResult(WebhookSubscription Subscription, string PlainSecret);
