using Granit.Privacy.DataExport.Events;
using Granit.Privacy.Diagnostics;
using Granit.Privacy.Options;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Granit.Privacy.DataExport;

/// <summary>
/// Stateful Saga implementing the privacy export scatter-gather pattern (GDPR Art. 15/20, LGPD Art. 18, CCPA).
/// </summary>
/// <remarks>
/// <para>
/// Flow:
/// <list type="number">
///   <item><see cref="PersonalDataRequestedEto"/> starts the Saga and schedules a timeout.</item>
///   <item>Each registered data provider handles the event, uploads its data fragment to
///   BlobStorage, and publishes a <see cref="PersonalDataPreparedEto"/> with the
///   <c>BlobReferenceId</c>.</item>
///   <item>The Saga collects fragments. When all expected fragments arrive it publishes
///   <see cref="ExportCompletedEto"/> with <c>IsPartial = false</c>.</item>
///   <item>If the timeout fires before all fragments arrive, the Saga publishes a partial
///   <see cref="ExportCompletedEto"/> with <c>IsPartial = true</c> listing missing providers.</item>
/// </list>
/// </para>
/// <para>
/// ISO 27001 compliance: fragments are referenced by <c>BlobReferenceId</c> only — raw personal data
/// is never stored in the Saga state or event payloads.
/// </para>
/// <para>
/// The <c>ArchiveBlobReferenceId</c> in <see cref="ExportCompletedEto"/> uses the convention
/// <c>"personal-data-export/{RequestId}"</c>. The application assembles the final archive under this key
/// using the individual fragment references.
/// </para>
/// </remarks>
public sealed class PersonalDataExportSaga : Saga
{
    /// <summary>Saga correlation ID — equals <see cref="PersonalDataRequestedEto.RequestId"/>.</summary>
    public Guid Id { get; set; }

    /// <summary>User whose data is being exported.</summary>
    public Guid UserId { get; set; }

    /// <summary>Number of fragments expected (from <see cref="IDataProviderRegistry.Count"/>).</summary>
    public int ExpectedCount { get; set; }

    /// <summary>Fragments received from data providers (BlobReferenceId only — ISO 27001).</summary>
    public List<ReceivedFragment> ReceivedFragments { get; set; } = [];

    /// <summary>Provider names that have not yet responded.</summary>
    public List<string> PendingProviders { get; set; } = [];

    /// <summary>Applicable privacy regulation code for this export request.</summary>
    public string Regulation { get; set; } = string.Empty;

    /// <summary>Tenant identifier propagated from the starting event for metrics tagging.</summary>
    public string? TenantId { get; set; }

    /// <summary>When the data subject filed the export request — propagated to <see cref="ExportCompletedEto"/>.</summary>
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>
    /// Starts the Saga when a data subject requests export of their personal data.
    /// If no providers are registered, completes immediately.
    /// Otherwise, schedules a timeout to handle unresponsive providers.
    /// </summary>
    // NOTE: Named `Start` (no `Async` suffix) so Wolverine's SagaChain discovers it.
    // SagaChain.findByNames is strict-match and does NOT strip `Async`, unlike general
    // handler discovery. An `Async`-suffixed name compiles to a silent no-op handler.
    public async Task<ExportCompletedEto?> Start(
        PersonalDataRequestedEto @event,
        IDataProviderRegistry registry,
        IOptions<GranitPrivacyOptions> options,
        IMessageContext context,
        PrivacyMetrics metrics)
    {
        Id = @event.RequestId;
        UserId = @event.UserId;
        Regulation = @event.Regulation;
        TenantId = @event.TenantId;
        RequestedAt = @event.RequestedAt;
        ExpectedCount = registry.Count;
        metrics.RecordExportRequested(TenantId, Regulation);
        PendingProviders = [.. registry.GetAll()];

        if (ExpectedCount == 0)
        {
            MarkCompleted();
            return new ExportCompletedEto(
                Id,
                UserId,
                $"personal-data-export/{Id}",
                IsPartial: false,
                MissingProviders: [],
                Fragments: [],
                Regulation,
                RequestedAt);
        }

        await context.ScheduleAsync(
            new ExportTimedOutEvent(@event.RequestId),
            TimeSpan.FromMinutes(options.Value.ExportTimeoutMinutes)).ConfigureAwait(false);

        return null;
    }

    /// <summary>
    /// Handles a fragment prepared by a data provider.
    /// Completes the Saga if all expected fragments have been received.
    /// </summary>
    public ExportCompletedEto? Handle(PersonalDataPreparedEto @event, PrivacyMetrics metrics)
    {
        ReceivedFragments.Add(new ReceivedFragment(
            @event.ProviderName,
            @event.FragmentKind,
            @event.SourceContainer,
            @event.BlobReferenceId,
            @event.EntryPath,
            @event.ContentType,
            @event.IntegrityTag));
        bool expected = PendingProviders.Remove(@event.ProviderName);
        metrics.RecordFragmentReceived(TenantId, expected ? @event.ProviderName : "unknown", Regulation);

        // Multi-fragment providers (Documents, attachments) emit one Prepared event per
        // fragment but only count once against ExpectedCount via PendingProviders.Remove.
        // We must wait until every expected provider has stopped contributing — which we
        // approximate by waiting until PendingProviders is drained.
        if (PendingProviders.Count > 0)
        {
            return null;
        }

        MarkCompleted();
        return new ExportCompletedEto(
            Id,
            UserId,
            PrivacyExportContainerNames.ArchiveBlobReferenceId(Id),
            IsPartial: false,
            MissingProviders: [],
            Fragments: ReceivedFragments.AsReadOnly(),
            Regulation,
            RequestedAt);
    }

    /// <summary>
    /// Handles the timeout event.
    /// Publishes a partial <see cref="ExportCompletedEto"/> with whatever fragments arrived.
    /// </summary>
    public ExportCompletedEto Handle(ExportTimedOutEvent @event, PrivacyMetrics metrics)
    {
        metrics.RecordExportCompleted(TenantId, "timeout", TimeSpan.Zero, Regulation);
        MarkCompleted();
        return new ExportCompletedEto(
            Id,
            UserId,
            PrivacyExportContainerNames.ArchiveBlobReferenceId(Id),
            IsPartial: true,
            MissingProviders: PendingProviders.AsReadOnly(),
            Fragments: ReceivedFragments.AsReadOnly(),
            Regulation,
            RequestedAt);
    }
}
