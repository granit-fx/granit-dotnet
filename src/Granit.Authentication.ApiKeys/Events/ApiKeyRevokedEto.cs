using Granit.Events;

namespace Granit.Authentication.ApiKeys.Events;

/// <summary>
/// Published when an API key is revoked. Consumed by cache invalidation
/// handlers across all instances (via Wolverine outbox).
/// </summary>
/// <param name="ApiKeyId">The unique identifier of the revoked key.</param>
/// <param name="HashedKey">The SHA-256 hash of the key for cache eviction.</param>
public sealed record ApiKeyRevokedEto(Guid ApiKeyId, string HashedKey) : IIntegrationEvent;
