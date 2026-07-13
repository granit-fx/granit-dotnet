using Granit.Privacy.DataDeletion.Events;
using Wolverine.Persistence.Sagas;

namespace Granit.Privacy.Wolverine;

/// <summary>
/// Internal saga timeout event: fires when the grace period expires
/// and triggers the actual deletion by publishing <see cref="PersonalDataDeletionRequestedEto"/>.
/// </summary>
public sealed record DeletionDeadlineReachedEvent([property: SagaIdentity] Guid RequestId);
