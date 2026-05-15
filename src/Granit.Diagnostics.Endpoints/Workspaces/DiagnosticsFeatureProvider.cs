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
/// Ships alongside the legacy <see cref="DiagnosticsWorkspaceContribution"/>
/// during the phase 3 migration window — both surfaces work, both produce
/// the same UI entry. Defaults bundles (phase 2) consume the feature; the
/// legacy contribution is removed in phase 5.
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
