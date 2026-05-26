using Granit.Domain.ValueObjects;
using Granit.Events;
using Wolverine.Persistence.Sagas;

namespace Granit.Privacy.DataExport.Events;

/// <summary>
/// Published by each data provider after preparing a fragment for a data export request.
/// Carries the pointer the assembler needs to materialise the entry in the final archive
/// plus the HMAC capability tag the assembler verifies before opening the source stream.
/// </summary>
/// <remarks>
/// Raw bytes are never included in the event payload (ISO 27001 — fragment content stays
/// in BlobStorage). A provider may publish more than one event for the same request when
/// it yields multiple fragments (blob-backed providers — Documents, attachments).
/// </remarks>
public sealed record PersonalDataPreparedEto(
    [property: SagaIdentity] Guid RequestId,
    string ProviderName,
    string FragmentKind,
    string SourceContainer,
    BlobReference BlobReferenceId,
    string EntryPath,
    string ContentType,
    string IntegrityTag,
    Guid? TenantId = null) : IIntegrationEvent;
