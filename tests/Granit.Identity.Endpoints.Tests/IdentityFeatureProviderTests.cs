using Granit.Identity.Endpoints.Permissions;
using Granit.Identity.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

public sealed class IdentityFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_users_roles_groups_sessions()
    {
        FakeCatalog catalog = new();
        new IdentityFeatureProvider().DefineFeatures(catalog);

        catalog.Get(IdentityFeatures.Users).Permission.ShouldBe(IdentityPermissions.Users.Read);
        catalog.Get(IdentityFeatures.Roles).Permission.ShouldBe(IdentityPermissions.Roles.Read);
        catalog.Get(IdentityFeatures.Groups).Permission.ShouldBe(IdentityPermissions.Groups.Read);
        catalog.Get(IdentityFeatures.Sessions).Permission.ShouldBe(IdentityPermissions.Sessions.Read);

        catalog.Get(IdentityFeatures.Users).RouteName.ShouldBe(IdentityFeatures.Users);
        catalog.Get(IdentityFeatures.Users).DefaultIcon.ShouldBe("users-round");
        catalog.Get(IdentityFeatures.Users).DisplayKey.ShouldBe("IdentityEndpoints:Workspace.Users");
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
