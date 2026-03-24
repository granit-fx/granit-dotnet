using Granit.Events;
using Wolverine.Persistence.Sagas;

namespace Granit.Privacy.DataExport.Events;

/// <summary>
/// Published by each data provider after preparing its fragment for a data export request.
/// The <see cref="BlobReferenceId"/> points to the fragment stored in BlobStorage —
/// raw data is never included in the event payload (ISO 27001 compliance).
/// </summary>
public sealed record PersonalDataPreparedEto(
    [property: SagaIdentity] Guid RequestId,
    string ProviderName,
    string BlobReferenceId,
    string ContentType) : IIntegrationEvent;
