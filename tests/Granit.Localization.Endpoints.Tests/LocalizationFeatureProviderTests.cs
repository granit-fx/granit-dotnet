using Granit.Localization.Endpoints.Permissions;
using Granit.Localization.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.Localization.Endpoints.Tests;

public sealed class LocalizationFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new LocalizationFeatureProvider().DefineFeatures(catalog);

        catalog.Get(LocalizationFeatures.Overrides).Permission.ShouldBe(LocalizationOverridesPermissions.Overrides.Read);
        catalog.Get(LocalizationFeatures.Overrides).RouteName.ShouldBe(LocalizationFeatures.Overrides);
        catalog.Get(LocalizationFeatures.Overrides).DefaultIcon.ShouldBe("languages");
        catalog.Get(LocalizationFeatures.Overrides).DisplayKey.ShouldBe("LocalizationEndpoints:Workspace.Item");
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
