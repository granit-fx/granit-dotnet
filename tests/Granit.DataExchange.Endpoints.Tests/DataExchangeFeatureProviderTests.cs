using Granit.DataExchange.Endpoints.Permissions;
using Granit.DataExchange.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Endpoints.Tests;

public sealed class DataExchangeFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new DataExchangeFeatureProvider().DefineFeatures(catalog);

        catalog.Get(DataExchangeFeatures.Imports).Permission.ShouldBe(DataExchangePermissions.Imports.Read);
        catalog.Get(DataExchangeFeatures.Imports).RouteName.ShouldBe(DataExchangeFeatures.Imports);
        catalog.Get(DataExchangeFeatures.Imports).DefaultIcon.ShouldBe("download");
        catalog.Get(DataExchangeFeatures.Imports).DisplayKey.ShouldBe("DataExchangeEndpoints:Workspace.Imports");
        catalog.Get(DataExchangeFeatures.Exports).Permission.ShouldBe(DataExchangePermissions.Exports.Read);
        catalog.Get(DataExchangeFeatures.Exports).RouteName.ShouldBe(DataExchangeFeatures.Exports);
        catalog.Get(DataExchangeFeatures.Exports).DefaultIcon.ShouldBe("upload");
        catalog.Get(DataExchangeFeatures.Exports).DisplayKey.ShouldBe("DataExchangeEndpoints:Workspace.Exports");
    }

    private sealed class FakeCatalog : IFeatureCatalogBuilder
    {
        private readonly Dictionary<string, FeatureDescriptor> _byName = new(StringComparer.Ordinal);
        public void Add(string name, Action<FeatureBuilder> configure)
        {
            FeatureBuilder b = new(name);
            configure(b);
            _byName.Add(name, b.Build());
        }
        public FeatureDescriptor Get(string name) => _byName[name];
    }
}
