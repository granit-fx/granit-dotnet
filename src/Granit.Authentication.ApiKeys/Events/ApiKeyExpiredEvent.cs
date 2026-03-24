using Granit.Events;

namespace Granit.Authentication.ApiKeys.Events;

/// <summary>
/// Raised when an API key is detected as expired.
/// Enables security monitoring and automatic expiration enforcement.
/// </summary>
/// <param name="ApiKeyId">The unique identifier of the expired key.</param>
/// <param name="Prefix">Key prefix for identification (e.g., <c>gk_live_sk_</c>).</param>
/// <param name="ExpiredAt">The expiration timestamp of the key.</param>
public sealed record ApiKeyExpiredEvent(
    Guid ApiKeyId,
    string Prefix,
    DateTimeOffset ExpiredAt) : IDomainEvent;
