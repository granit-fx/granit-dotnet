using Wolverine.Persistence.Sagas;

namespace Granit.Privacy.DataExport.Events;

/// <summary>
/// Internal timeout event for the privacy export Saga.
/// Scheduled when the Saga starts; triggers partial export completion if not all fragments arrived.
/// </summary>
public sealed record ExportTimedOutEvent([property: SagaIdentity] Guid RequestId);
