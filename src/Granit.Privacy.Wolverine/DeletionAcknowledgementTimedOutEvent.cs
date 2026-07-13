using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataDeletion.Events;
using Wolverine.Persistence.Sagas;

namespace Granit.Privacy.Wolverine;

/// <summary>
/// Internal saga timeout event: scheduled when the deletion deadline is reached and the provider
/// fan-out begins. If it fires before every registered provider has acknowledged erasure (via
/// <see cref="PersonalDataDeletedEto"/>), the saga transitions the request to
/// <see cref="DeletionRequestState.PartiallyExecuted"/> and surfaces the missing providers so
/// operators can reconcile stuck / dead-lettered deletions (GDPR Art. 17 provability).
/// </summary>
public sealed record DeletionAcknowledgementTimedOutEvent([property: SagaIdentity] Guid RequestId);
