using Granit.BackgroundJobs;
using Granit.Modularity;
using Granit.Privacy.BackgroundJobs.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Privacy.BackgroundJobs;

/// <summary>
/// Granit module that registers background jobs for Privacy:
/// deletion deadline enforcement and personal-data export assembly.
/// </summary>
/// <remarks>
/// <para>
/// <b>Export assembly.</b> The package ships the
/// <c>PrivacyExportAssemblyJob</c> + handler shape but does not own the
/// concrete <c>IPrivacyExportAssemblyService</c> implementation — that lives in
/// <c>Granit.Privacy.BlobStorage</c>, registered by
/// <c>AddGranitPrivacyBlobStorage()</c>. Hosts that want async sharded assembly
/// wire both modules.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitPrivacyModule))]
public sealed class GranitPrivacyBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddTransient<DeletionDeadlineEnforcementService>();
}
