using Granit.DataExchange.Endpoints.Permissions;
using Granit.Workspaces;

namespace Granit.DataExchange.Endpoints.Workspaces;

/// <summary>Declares the data-exchange module's features (per ADR-057).</summary>
internal sealed class DataExchangeFeatureProvider : IFeatureProvider
{
    /// <inheritdoc />
    public void DefineFeatures(IFeatureCatalogBuilder catalog)
    {
        catalog.Add(DataExchangeFeatures.Imports, f => f
            .Permission(DataExchangePermissions.Imports.Read)
            .RouteName(DataExchangeFeatures.Imports)
            .DefaultIcon("download")
            .DisplayKey("DataExchangeEndpoints:Workspace.Imports"));

        catalog.Add(DataExchangeFeatures.Exports, f => f
            .Permission(DataExchangePermissions.Exports.Read)
            .RouteName(DataExchangeFeatures.Exports)
            .DefaultIcon("upload")
            .DisplayKey("DataExchangeEndpoints:Workspace.Exports"));
    }
}
