using Granit.Core.Events;

namespace Granit.OpenIddict.Events;

/// <summary>
/// Integration event published when an administrator impersonates a user.
/// </summary>
/// <remarks>
/// Consumed by <c>Granit.Notifications</c> to send a transparency notification
/// to the impersonated user (GDPR / SOC2 compliance).
/// </remarks>
/// <param name="TargetUserId">The user being impersonated.</param>
/// <param name="ImpersonatorId">The administrator performing the impersonation.</param>
/// <param name="TenantId">The tenant identifier, or <see langword="null"/> for global users.</param>
/// <param name="OccurredAt">The UTC timestamp of the impersonation.</param>
public sealed record UserImpersonatedEto(
    Guid TargetUserId,
    Guid ImpersonatorId,
    Guid? TenantId,
    DateTimeOffset OccurredAt) : IIntegrationEvent;
