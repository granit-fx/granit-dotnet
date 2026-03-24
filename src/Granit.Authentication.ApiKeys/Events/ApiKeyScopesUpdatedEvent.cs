using Granit.Events;

namespace Granit.Authentication.ApiKeys.Events;

/// <summary>
/// Published when the permissions of an API key are modified.
/// Triggers cache invalidation so that the new scopes take effect.
/// </summary>
/// <param name="ApiKeyId">The identifier of the updated key.</param>
/// <param name="HashedKey">The hash of the key for cache eviction.</param>
public sealed record ApiKeyScopesUpdatedEto(Guid ApiKeyId, string HashedKey) : IIntegrationEvent;
