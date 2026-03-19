using Granit.Core.Events;

namespace Granit.Authentication.ApiKeys.Events;

/// <summary>
/// Published when an API key is used for authentication.
/// Enables usage tracking and ISO 27001 compliance auditing.
/// </summary>
/// <param name="ApiKeyId">The unique identifier of the key.</param>
/// <param name="Prefix">Key prefix for identification (e.g., <c>gk_live_sk_</c>).</param>
/// <param name="UsedAt">Timestamp of the API call.</param>
public sealed record ApiKeyUsedEto(
    Guid ApiKeyId,
    string Prefix,
    DateTimeOffset UsedAt) : IIntegrationEvent;
