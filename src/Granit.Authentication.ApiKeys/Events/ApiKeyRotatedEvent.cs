using Granit.Core.Events;

namespace Granit.Authentication.ApiKeys.Events;

/// <summary>
/// Published when an API key is rotated. The old key remains active during
/// the grace period.
/// </summary>
/// <param name="OldApiKeyId">The identifier of the key being replaced.</param>
/// <param name="NewApiKeyId">The identifier of the newly created key.</param>
/// <param name="OldHashedKey">The hash of the old key for cache eviction after the grace period.</param>
public sealed record ApiKeyRotatedEto(Guid OldApiKeyId, Guid NewApiKeyId, string OldHashedKey) : IIntegrationEvent;
