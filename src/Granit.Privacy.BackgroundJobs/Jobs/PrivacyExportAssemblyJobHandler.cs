using Granit.MultiTenancy;
using Granit.Privacy.DataExport;

namespace Granit.Privacy.BackgroundJobs.Jobs;

/// <summary>
/// Wolverine handler for <see cref="PrivacyExportAssemblyJob"/>. Opens the tenant
/// scope carried by the job, then delegates the sharded assembly to the
/// <see cref="IPrivacyExportAssemblyService"/> impl wired by the host (default impl
/// lives in <c>Granit.Privacy.BlobStorage</c>).
/// </summary>
/// <remarks>
/// <para>
/// Wolverine discovers handlers via <c>Assembly.ExportedTypes</c> — the class is
/// <c>public</c> with a public constructor, the method is <c>public static</c>.
/// See CLAUDE.md §Wolverine handlers.
/// </para>
/// <para>
/// <b>Tenant discipline.</b> The <c>using</c> on
/// <see cref="ICurrentTenant.Change"/> wraps service resolution and the entire
/// assembly run, so every scoped service (checkpoint store, tracker writer, blob
/// reads) sees the right tenant id. A bug here leaks data across tenants — guarded
/// by the cross-tenant isolation integration test in
/// <c>Granit.Privacy.BackgroundJobs.Tests.Integration</c>.
/// </para>
/// </remarks>
public sealed class PrivacyExportAssemblyJobHandler
{
    public static async Task HandleAsync(
        PrivacyExportAssemblyJob job,
        ICurrentTenant currentTenant,
        IPrivacyExportAssemblyService service,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(currentTenant);
        ArgumentNullException.ThrowIfNull(service);

        using IDisposable _ = currentTenant.Change(job.TenantId);
        await service.AssembleAsync(job.Event, cancellationToken).ConfigureAwait(false);
    }
}
