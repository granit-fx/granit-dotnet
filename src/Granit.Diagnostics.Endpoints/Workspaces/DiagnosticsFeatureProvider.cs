using Granit.Diagnostics.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Diagnostics.Endpoints.Workspaces;

/// <summary>
/// Declares the diagnostics module's features for the host's workspace
/// composition (per ADR-057). One feature today — the aggregated health
/// dashboard reachable at <c>/diagnostics</c> — gated by
/// <see cref="DiagnosticsPermissions.Monitoring"/>. A host that wants the
/// dashboard in its admin UI references this feature by name when
/// composing its workspace tree (e.g. <c>section.Feature(DiagnosticsFeatures.Monitoring)</c>).
/// </summary>
/// <remarks>
/// Replaced the legacy <c>DiagnosticsWorkspaceContribution</c> — the
/// framework workspace shells (per ADR-057 §4) now compose features
/// directly, so the auto-contributor mechanism is no longer needed for
/// this module.
/// </remarks>
internal sealed class DiagnosticsFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog) =>
        catalog.Add(DiagnosticsFeatures.Monitoring, f => f
            .Permission(DiagnosticsPermissions.Monitoring.Read)
            .RouteName(DiagnosticsFeatures.Monitoring)
            .DefaultIcon("heart-pulse")
            .DisplayKey("DiagnosticsEndpoints:Workspace.Item"));
}
