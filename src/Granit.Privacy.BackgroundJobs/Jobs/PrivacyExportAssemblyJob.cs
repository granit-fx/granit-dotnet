using Granit.BackgroundJobs;
using Granit.Privacy.DataExport.Events;

namespace Granit.Privacy.BackgroundJobs.Jobs;

/// <summary>
/// On-demand background job that assembles the personal-data export archive(s) for
/// a single GDPR-Art.20 request. Dispatched by the saga's terminal handler after
/// every provider has prepared its fragment(s); does the sharded ZIP assembly,
/// writes per-shard checkpoints, and updates the request tracker.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not auto-scheduled.</b> No <see cref="RecurringJobAttribute"/> applies — assembly
/// fires per request, not on a cron. Hosts dispatch via
/// <c>IBackgroundJobDispatcher.PublishAsync</c> from the
/// <see cref="ExportCompletedEto"/> handler (wired in a follow-up sub-PR; until then
/// the job is callable from custom code or tests).
/// </para>
/// <para>
/// <b>Tenant scope.</b> Background workers run outside any HTTP / message context,
/// so <see cref="Granit.MultiTenancy.ICurrentTenant"/> defaults to "no tenant". The
/// handler MUST open <c>currentTenant.Change(TenantId)</c> before resolving scoped
/// services — otherwise EF tenant filters silently leak (
/// <c>project_current_tenant_asynclocal_leak</c>).
/// </para>
/// <para>
/// <b>Payload.</b> The job carries the saga-published <see cref="ExportCompletedEto"/>
/// verbatim — fragments, missing-providers list, regulation, and timestamps. Embedding
/// the event avoids a follow-up DB read and lets a re-dispatched job operate without
/// the saga state still being live (which would race the saga's own cleanup).
/// </para>
/// </remarks>
public sealed record PrivacyExportAssemblyJob(ExportCompletedEto Event) : IBackgroundJob
{
    /// <summary>Convenience accessor — also the saga correlation id.</summary>
    public Guid RequestId => Event.RequestId;

    /// <summary>Tenant scope for the handler to open before resolving scoped services.</summary>
    public Guid? TenantId => Event.TenantId;
}
