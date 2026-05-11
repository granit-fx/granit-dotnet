using Granit.Documents.PublicLinks.Domain;
using Granit.Events;

namespace Granit.Documents.PublicLinks.Events;

/// <summary>
/// Distributed integration event raised on every successful public-link redemption.
/// </summary>
/// <param name="LinkId">Identifier of the consumed link.</param>
/// <param name="TenantId">Tenant identifier of the link (links may live outside a tenant context).</param>
/// <param name="DocumentId">Identifier of the underlying document.</param>
/// <param name="Scope">Granted scope of the link.</param>
/// <param name="CurrentUses">Post-increment usage counter (number of successful redemptions so far).</param>
/// <param name="ConsumedAt">UTC instant the consumption was recorded.</param>
/// <param name="ClientIpMasked">
/// Optional anonymised client IP captured by the HTTP surface: /24 for IPv4 and /48 for IPv6.
/// The raw IP is never carried — only a network prefix sufficient for abuse-pattern detection.
/// </param>
/// <param name="UserAgent">
/// Optional <c>User-Agent</c> header value captured by the HTTP surface. Downstream consumers
/// SHOULD honour retention limits — UA strings can be quasi-identifiers under GDPR.
/// </param>
public sealed record DocumentPublicLinkConsumedEto(
    Guid LinkId,
    Guid? TenantId,
    Guid DocumentId,
    PublicLinkScope Scope,
    int CurrentUses,
    DateTimeOffset ConsumedAt,
    string? ClientIpMasked = null,
    string? UserAgent = null) : IIntegrationEvent;
