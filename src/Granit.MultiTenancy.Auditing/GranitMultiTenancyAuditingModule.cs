using Granit.Auditing;
using Granit.Modularity;
using Granit.MultiTenancy.Auditing.Extensions;

namespace Granit.MultiTenancy.Auditing;

/// <summary>
/// Granit module wiring host-impersonation events to <c>Granit.Auditing</c>.
/// Registering this module enables persistent audit-log entries for every
/// <c>IHostImpersonationGate</c> decision.
/// </summary>
[DependsOn(typeof(GranitAuditingModule))]
[DependsOn(typeof(GranitMultiTenancyModule))]
public sealed class GranitMultiTenancyAuditingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitHostImpersonationAuditing();
}
