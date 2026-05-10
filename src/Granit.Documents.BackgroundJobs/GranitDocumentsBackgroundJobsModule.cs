using Granit.BackgroundJobs;
using Granit.Documents;
using Granit.Modularity;

namespace Granit.Documents.BackgroundJobs;

/// <summary>
/// Granit module exposing the recurring jobs for Granit.Documents — orphan blob
/// cleanup (F9.1), empty-trash retention (F9.2), and tenant quota recompute (F9.3).
/// </summary>
/// <remarks>
/// The handlers consume <see cref="IDocumentMaintenanceService"/>, registered by
/// <c>Granit.Documents.EntityFrameworkCore</c>. Hosts must therefore also register
/// the EF Core companion module — this BackgroundJobs package keeps a soft dep on
/// the base <c>Granit.Documents</c> contract only.
/// </remarks>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitDocumentsModule))]
public sealed class GranitDocumentsBackgroundJobsModule : GranitModule;
