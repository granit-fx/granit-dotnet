using Granit.Privacy.DataExport.Events;
using Granit.Privacy.Options;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Granit.Privacy.DataExport;

/// <summary>
/// Wolverine Stateful Saga implementing the GDPR export scatter-gather pattern (RGPD Art. 15/20).
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
/// <c>"gdpr-export/{RequestId}"</c>. The application assembles the final archive under this key
/// using the individual fragment references.
/// </para>
/// </remarks>
public sealed class GdprExportSaga : Saga
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

    /// <summary>
    /// Starts the Saga when a data subject requests export of their personal data.
    /// If no providers are registered, completes immediately.
    /// Otherwise, schedules a timeout to handle unresponsive providers.
    /// </summary>
    public async Task<ExportCompletedEto?> StartAsync(
        PersonalDataRequestedEto @event,
        IDataProviderRegistry registry,
        IOptions<GranitPrivacyOptions> options,
        IMessageContext context)
    {
        Id = @event.RequestId;
        UserId = @event.UserId;
        ExpectedCount = registry.Count;
        PendingProviders = [.. registry.GetAll()];

        if (ExpectedCount == 0)
        {
            MarkCompleted();
            return new ExportCompletedEto(Id, UserId, $"gdpr-export/{Id}", IsPartial: false, []);
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
    public ExportCompletedEto? Handle(PersonalDataPreparedEto @event)
    {
        ReceivedFragments.Add(new ReceivedFragment(@event.ProviderName, @event.BlobReferenceId, @event.ContentType));
        PendingProviders.Remove(@event.ProviderName);

        if (ReceivedFragments.Count < ExpectedCount)
        {
            return null;
        }

        MarkCompleted();
        return new ExportCompletedEto(Id, UserId, $"gdpr-export/{Id}", IsPartial: false, []);
    }

    /// <summary>
    /// Handles the timeout event.
    /// Publishes a partial <see cref="ExportCompletedEto"/> with whatever fragments arrived.
    /// </summary>
    public ExportCompletedEto Handle(ExportTimedOutEvent @event)
    {
        MarkCompleted();
        return new ExportCompletedEto(
            Id,
            UserId,
            $"gdpr-export/{Id}",
            IsPartial: true,
            PendingProviders.AsReadOnly());
    }
}
