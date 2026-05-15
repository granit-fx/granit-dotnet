using Granit.Settings.Endpoints.Permissions;
using Granit.Settings.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.Settings.Endpoints.Tests;

public sealed class SettingsFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new SettingsFeatureProvider().DefineFeatures(catalog);

        catalog.Get(SettingsFeatures.Global).Permission.ShouldBe(SettingsPermissions.Global.Read);
        catalog.Get(SettingsFeatures.Global).RouteName.ShouldBe(SettingsFeatures.Global);
        catalog.Get(SettingsFeatures.Global).DefaultIcon.ShouldBe("settings-2");
        catalog.Get(SettingsFeatures.Global).DisplayKey.ShouldBe("SettingsEndpoints:Workspace.Global");
        catalog.Get(SettingsFeatures.Tenant).Permission.ShouldBe(SettingsPermissions.Tenant.Read);
        catalog.Get(SettingsFeatures.Tenant).RouteName.ShouldBe(SettingsFeatures.Tenant);
        catalog.Get(SettingsFeatures.Tenant).DefaultIcon.ShouldBe("building");
        catalog.Get(SettingsFeatures.Tenant).DisplayKey.ShouldBe("SettingsEndpoints:Workspace.Tenant");
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
