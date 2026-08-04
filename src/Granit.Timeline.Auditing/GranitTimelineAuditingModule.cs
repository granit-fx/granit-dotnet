using Granit.Auditing;
using Granit.Modularity;
using Granit.Timeline.Auditing.Extensions;

namespace Granit.Timeline.Auditing;

/// <summary>
/// Granit module that wires the Auditing bridge into the Timeline reader.
/// </summary>
/// <remarks>
/// Depends on both <see cref="GranitTimelineModule"/> and
/// <see cref="GranitAuditingModule"/>. Loading this module is sufficient — no
/// per-entity registration is required. Audit entries appear in the timeline
/// of any entity whose <c>EntityType</c>/<c>EntityId</c> tuple is referenced
/// by the audit log.
/// </remarks>
[DependsOn(
    typeof(GranitAuditingModule),
    typeof(GranitTimelineModule))]
public sealed class GranitTimelineAuditingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTimelineAuditing();
}
