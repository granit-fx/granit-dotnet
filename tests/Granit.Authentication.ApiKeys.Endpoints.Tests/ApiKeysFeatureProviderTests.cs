using Granit.Authentication.ApiKeys.Endpoints.Permissions;
using Granit.Authentication.ApiKeys.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Endpoints.Tests;

public sealed class ApiKeysFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_keys_feature()
    {
        FakeCatalog catalog = new();
        new ApiKeysFeatureProvider().DefineFeatures(catalog);

        FeatureDescriptor keys = catalog.Get(ApiKeysFeatures.Keys);
        keys.Permission.ShouldBe(ApiKeyPermissions.Keys.Read);
        keys.RouteName.ShouldBe(ApiKeysFeatures.Keys);
        keys.DefaultIcon.ShouldBe("key-round");
        keys.DisplayKey.ShouldBe("AuthenticationApiKeysEndpoints:Workspace.Item");
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
