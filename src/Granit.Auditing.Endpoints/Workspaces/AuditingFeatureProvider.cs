using Granit.Auditing.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.Auditing.Endpoints.Workspaces;

/// <summary>Declares the auditing module's features (per ADR-057).</summary>
internal sealed class AuditingFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog) =>
        catalog.Add(AuditingFeatures.Entries, f => f
            .Permission(AuditingPermissions.AuditEntries.Read)
            .RouteName(AuditingFeatures.Entries)
            .DefaultIcon("clipboard-list")
            .DisplayKey("AuditingEndpoints:Workspace.Item"));
}
