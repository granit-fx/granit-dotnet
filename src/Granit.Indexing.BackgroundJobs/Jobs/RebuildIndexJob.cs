using Granit.BackgroundJobs;

namespace Granit.Indexing.BackgroundJobs.Jobs;

/// <summary>
/// On-demand background job that triggers a full rebuild of the index for one
/// <typeparamref name="TKey"/> + tenant combination. Resumes past the last checkpoint
/// when a prior run was interrupted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not auto-scheduled.</b> No <see cref="RecurringJobAttribute"/> is applied — that
/// attribute is reserved for cron-driven recurring jobs and its constructor rejects a
/// null cron. Hosts dispatch this job explicitly via
/// <c>IBackgroundJobDispatcher.PublishAsync</c> after a schema upgrade, tenant
/// onboarding, or operator intervention. Hosts that want a periodic full rebuild
/// (e.g. nightly re-tokenisation) wire their own cron-driven trigger that emits
/// <see cref="RebuildIndexJob{TKey}"/>.
/// </para>
/// <para>
/// <b>Authorization.</b> The handler trusts <c>TenantId</c> as-is — there is no
/// runtime principal at handler time. The dispatch site MUST enforce
/// <see cref="Granit.Indexing.BackgroundJobs.Permissions.IndexingRebuildPermissions.Rebuild.Execute"/>
/// (tenant-scoped), and additionally
/// <see cref="Granit.Indexing.BackgroundJobs.Permissions.IndexingRebuildPermissions.Rebuild.ExecuteGlobal"/>
/// when dispatching with <c>TenantId == null</c> (cross-tenant rebuild).
/// </para>
/// </remarks>
public sealed record RebuildIndexJob<TKey>(Guid? TenantId) : IBackgroundJob
    where TKey : notnull;
