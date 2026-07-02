using Granit.Events;
using Wolverine.Persistence.Sagas;

namespace Granit.Privacy.DataDeletion.Events;

/// <summary>
/// Published by each data provider after erasing (or crypto-shredding / anonymising / retaining)
/// its slice of a data subject's personal data in response to a
/// <see cref="PersonalDataDeletionRequestedEto"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the provider's <b>acknowledgement</b> in the deletion fan-in — the deletion counterpart
/// of the export saga's <c>PersonalDataPreparedEto</c>. <see cref="RequestId"/> carries
/// <c>[SagaIdentity]</c> so Wolverine routes the event back to the originating
/// <c>PersonalDataDeletionSaga</c>, which only marks the request
/// <see cref="DeletionRequestState.Executed"/> once every registered provider has acknowledged.
/// It doubles as the ISO 27001 audit record of what each provider actually did.
/// </para>
/// <para>
/// Every provider handling <see cref="PersonalDataDeletionRequestedEto"/> MUST publish exactly one
/// of these per request once its work is durably committed, using the provider name it registered
/// with <c>AddPrivacyDataProvider</c>. Wolverine's at-least-once delivery means a provider may
/// emit it more than once on retry; the saga's fan-in is idempotent (a duplicate acknowledgement
/// is a no-op).
/// </para>
/// </remarks>
public sealed record PersonalDataDeletedEto(
    [property: SagaIdentity] Guid RequestId,
    string ProviderName,
    DeletionAction Action,
    int AffectedRecords,
    string? Details,
    Guid? TenantId = null) : IIntegrationEvent;
