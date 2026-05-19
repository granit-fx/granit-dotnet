using Granit.Events;

namespace Granit.AI.Tenancy;

/// <summary>
/// Raised in-process when a workspace's <c>ApiKey</c> or <c>Endpoint</c> override
/// is updated or cleared. Consumed by per-provider client caches to evict the
/// previous SDK client immediately rather than wait for the sliding-expiration TTL.
/// </summary>
/// <remarks>
/// <para>
/// In-process, transactional (cf <see cref="IDomainEvent"/>). Emitted from the
/// workspace credential update flow alongside the entity write.
/// </para>
/// <para>
/// The new <c>ApiKey</c> is intentionally NOT carried by this event — handlers only need to
/// know that the previous client is stale. Avoiding plaintext in the event envelope means
/// the in-process bus, the Wolverine outbox (should the event ever be routed externally),
/// and any persisted event audit do not duplicate credential material.
/// </para>
/// </remarks>
/// <param name="WorkspaceName">Name of the workspace whose credentials changed.</param>
/// <param name="Provider">Provider identifier (<c>Anthropic</c>, <c>OpenAI</c>, ...).</param>
/// <param name="TenantId">Owning tenant of the workspace, or <c>null</c> for system scope.</param>
public sealed record AIWorkspaceCredentialsChangedEvent(
    string WorkspaceName,
    string Provider,
    Guid? TenantId) : IDomainEvent;
