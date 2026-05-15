using Granit.MultiTenancy.Endpoints.Permissions;
using Granit.MultiTenancy.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Endpoints.Tests;

public sealed class MultiTenancyFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new MultiTenancyFeatureProvider().DefineFeatures(catalog);

        catalog.Get(MultiTenancyFeatures.Tenants).Permission.ShouldBe(MultiTenancyPermissions.Tenants.Read);
        catalog.Get(MultiTenancyFeatures.Tenants).RouteName.ShouldBe(MultiTenancyFeatures.Tenants);
        catalog.Get(MultiTenancyFeatures.Tenants).DefaultIcon.ShouldBe("building-2");
        catalog.Get(MultiTenancyFeatures.Tenants).DisplayKey.ShouldBe("MultiTenancyEndpoints:Workspace.Tenants");
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
