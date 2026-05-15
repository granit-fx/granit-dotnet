using Granit.Diagnostics.Endpoints.Permissions;
using Granit.Diagnostics.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.Diagnostics.Endpoints.Tests;

public sealed class DiagnosticsFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_monitoring_feature_with_expected_metadata()
    {
        FakeFeatureCatalogBuilder catalog = new();
        DiagnosticsFeatureProvider provider = new();

        provider.DefineFeatures(catalog);

        FeatureDescriptor feature = catalog.Single(DiagnosticsFeatures.Monitoring);
        feature.Name.ShouldBe(DiagnosticsFeatures.Monitoring);
        feature.Permission.ShouldBe(DiagnosticsPermissions.Monitoring.Read);
        feature.RouteName.ShouldBe(DiagnosticsFeatures.Monitoring);
        feature.DefaultIcon.ShouldBe("heart-pulse");
        feature.DisplayKey.ShouldBe("DiagnosticsEndpoints:Workspace.Item");
    }

    private sealed class FakeFeatureCatalogBuilder : IFeatureCatalogBuilder
    {
        private readonly Dictionary<string, FeatureDescriptor> _byName = new(StringComparer.Ordinal);

        public void Add(string name, Action<FeatureBuilder> configure)
        {
            FeatureBuilder builder = new(name);
            configure(builder);
            _byName.Add(name, builder.Build());
        }

        public FeatureDescriptor Single(string name) => _byName[name];
    }
}
