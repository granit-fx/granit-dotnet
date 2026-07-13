using Granit.Events;

namespace Granit.Privacy.DataDeletion.Events;

/// <summary>
/// Published by <c>PersonalDataDeletionSaga</c> (in <c>Granit.Privacy.Wolverine</c>) when the reminder timeout fires.
/// Consumed by <c>Granit.Privacy.Notifications</c> to send a reminder email.
/// </summary>
public sealed record DeletionReminderDueEto(
    Guid RequestId,
    Guid UserId,
    DateTimeOffset ScheduledDeletionAt) : IIntegrationEvent;
