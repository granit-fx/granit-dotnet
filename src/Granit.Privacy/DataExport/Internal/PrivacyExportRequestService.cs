using Granit.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Privacy.DataExport.Audit;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.Diagnostics;

namespace Granit.Privacy.DataExport.Internal;

/// <summary>
/// Default <see cref="IPrivacyExportRequestService"/> orchestrating the export workflow. The
/// tracker is optional so a host that only references the module can still construct the service;
/// hosts that map the export endpoints wire it via <c>UseExportRequestTracker</c>. The audit
/// writer and subject validator always have framework defaults registered.
/// </summary>
internal sealed class PrivacyExportRequestService(
    IDistributedEventBus eventBus,
    IPrivacyExportAuditWriter auditWriter,
    IPrivacySubjectValidator subjectValidator,
    PrivacyMetrics metrics,
    TimeProvider timeProvider,
    ICurrentTenant currentTenant,
    IGuidGenerator guidGenerator,
    IExportRequestTrackerWriter? tracker = null) : IPrivacyExportRequestService
{
    public async Task<RequestExportOutcome> RequestExportAsync(
        RequestExportCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Tenant-bound subject existence check (admin DSR only). A subject the caller cannot see
        // in its current tenant is reported as not-found (NOT forbidden) — same shape as a
        // non-existent request id, so cross-tenant subject probing yields no signal.
        if (command.ValidateSubject)
        {
            bool subjectExists = await subjectValidator
                .SubjectExistsInCurrentTenantAsync(command.SubjectUserId, cancellationToken)
                .ConfigureAwait(false);
            if (!subjectExists)
            {
                return new RequestExportOutcome(RequestExportResult.SubjectNotFound, Guid.Empty, default);
            }
        }

        Guid requestId = guidGenerator.Create();
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        // Null / empty scopes → "everything visible" (Takeout default). Unknown / hidden entries
        // are dropped by the saga via the visibility resolver.
        IReadOnlyList<string>? requestedScopes = command.Scopes is { Count: > 0 } scopes ? scopes : null;

        // Self-service collapses caller == subject to a null CallerUserId on the tracker row.
        if (tracker is not null)
        {
            await tracker
                .RecordRequestAsync(requestId, command.SubjectUserId, command.CallerUserId, now, cancellationToken)
                .ConfigureAwait(false);
        }

        // The saga's PersonalDataRequestedEto.UserId names the data subject — providers and the
        // visibility resolver key off it for HasData probes etc.
        await eventBus
            .PublishAsync(
                new PersonalDataRequestedEto(
                    RequestId: requestId,
                    UserId: command.SubjectUserId,
                    RequestedAt: now,
                    Regulation: command.Regulation,
                    TenantId: tenantId,
                    RequestedScopes: requestedScopes),
                cancellationToken)
            .ConfigureAwait(false);

        metrics.RecordExportRequested(tenantId, command.Regulation);

        await auditWriter.WriteExportRequestedAsync(
            new PrivacyExportRequestedAudit(
                RequestId: requestId,
                CallerUserId: command.CallerUserId,
                SubjectUserId: command.SubjectUserId,
                TenantId: tenantId,
                Regulation: command.Regulation,
                ResolvedScopes: requestedScopes ?? [],
                ClientIp: command.Audit.ClientIp,
                UserAgent: command.Audit.UserAgent,
                CorrelationId: command.Audit.CorrelationId,
                Timestamp: now),
            cancellationToken).ConfigureAwait(false);

        return new RequestExportOutcome(RequestExportResult.Accepted, requestId, now);
    }
}
