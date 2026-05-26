using Granit.BackgroundJobs;

namespace Granit.Indexing.BackgroundJobs.Jobs;

/// <summary>
/// On-demand background job that triggers a full rebuild of the index for one
/// <typeparamref name="TKey"/> + tenant combination. Resumes past the last checkpoint
/// when a prior run was interrupted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not auto-scheduled.</b> The <see cref="RecurringJobAttribute"/> carries a
/// <c>null</c> cron — hosts dispatch the job explicitly via
/// <c>IBackgroundJobDispatcher.PublishAsync</c> after a schema upgrade, tenant
/// onboarding, or operator intervention.
/// </para>
/// <para>
/// <b>Name.</b> <c>indexing-rebuild-index</c> per the framework
/// <c>{module-kebab}-{action-kebab}</c> convention. The same name covers every
/// <typeparamref name="TKey"/> instantiation — the message type carries the
/// discriminator.
/// </para>
/// </remarks>
[RecurringJob(cronExpression: null!, name: "indexing-rebuild-index")]
public sealed record RebuildIndexJob<TKey>(Guid? TenantId) : IBackgroundJob
    where TKey : notnull;
