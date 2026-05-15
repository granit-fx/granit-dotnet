using Granit.Authorization.Endpoints.Permissions;
using Granit.Authorization.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Endpoints.Tests;

public sealed class AuthorizationFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_permissions_feature()
    {
        FakeCatalog catalog = new();
        new AuthorizationFeatureProvider().DefineFeatures(catalog);

        FeatureDescriptor permissions = catalog.Get(AuthorizationFeatures.Permissions);
        permissions.Permission.ShouldBe(AuthorizationEndpointsPermissions.Definitions.Read);
        permissions.RouteName.ShouldBe(AuthorizationFeatures.Permissions);
        permissions.DefaultIcon.ShouldBe("key");
        permissions.DisplayKey.ShouldBe("AuthorizationEndpoints:Workspace.Definitions");
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
