using Granit.Events;
using Wolverine.Persistence.Sagas;

namespace Granit.Privacy.DataDeletion.Events;

/// <summary>
/// Published when a user cancels a deferred deletion request during the grace period.
/// Correlated to <c>PersonalDataDeletionSaga</c> (in <c>Granit.Privacy.Wolverine</c>) via <see cref="RequestId"/>.
/// </summary>
public sealed record DeletionCancelledEto(
    [property: SagaIdentity] Guid RequestId,
    Guid UserId,
    DateTimeOffset CancelledAt) : IIntegrationEvent;
