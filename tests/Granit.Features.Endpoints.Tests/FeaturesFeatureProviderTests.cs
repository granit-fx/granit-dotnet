using Granit.Features.Endpoints.Permissions;
using Granit.Features.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.Features.Endpoints.Tests;

public sealed class FeaturesFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new FeaturesFeatureProvider().DefineFeatures(catalog);

        catalog.Get(FeaturesFeatures.Flags).Permission.ShouldBe(FeaturesPermissions.Flags.Read);
        catalog.Get(FeaturesFeatures.Flags).RouteName.ShouldBe(FeaturesFeatures.Flags);
        catalog.Get(FeaturesFeatures.Flags).DefaultIcon.ShouldBe("flag");
        catalog.Get(FeaturesFeatures.Flags).DisplayKey.ShouldBe("FeaturesEndpoints:Workspace.Item");
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
