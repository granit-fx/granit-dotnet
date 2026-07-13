using Wolverine.Persistence.Sagas;

namespace Granit.Privacy.Wolverine;

/// <summary>
/// Internal saga timeout event: fires N days before the deletion deadline
/// to trigger a reminder notification to the user.
/// </summary>
public sealed record DeletionReminderDueEvent([property: SagaIdentity] Guid RequestId);
