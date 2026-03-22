using Granit.Core.Events;

namespace Granit.Privacy.DataDeletion.Events;

/// <summary>
/// Published when personal data deletion is executed — either immediately (no defer)
/// or when the grace period deadline expires. Consumed by <c>Granit.Privacy.Notifications</c>
/// to send a confirmation email.
/// </summary>
/// <remarks>
/// This is distinct from <see cref="PersonalDataDeletionRequestedEto"/> (provider-facing)
/// and <see cref="PersonalDataDeletedEto"/> (per-provider audit). This event is
/// user-facing and triggers the confirmation notification.
/// </remarks>
public sealed record DeletionExecutedEto(
    Guid RequestId,
    Guid UserId,
    DateTimeOffset ExecutedAt) : IIntegrationEvent;
