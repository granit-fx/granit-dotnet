using Granit.Core.Events;

namespace Granit.Authentication.ApiKeys.Events;

/// <summary>
/// Published when an API key is used for an API call.
/// Provides a security audit trail for ISO 27001 compliance — usage tracking.
/// </summary>
/// <param name="ApiKeyId">The unique identifier of the used key.</param>
/// <param name="Prefix">The key prefix for identification (e.g., <c>gk_live_sk_</c>).</param>
/// <param name="UsedAt">The timestamp when the key was used.</param>
public sealed record ApiKeyUsedEto(
    Guid ApiKeyId,
    string Prefix,
    DateTimeOffset UsedAt) : IIntegrationEvent;
