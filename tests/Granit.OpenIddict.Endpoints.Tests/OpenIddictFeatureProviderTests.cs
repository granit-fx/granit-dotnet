using Granit.OpenIddict.Endpoints.Workspaces;
using Granit.OpenIddict.Permissions;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Endpoints.Tests;

public sealed class OpenIddictFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_applications_scopes_authorizations()
    {
        FakeCatalog catalog = new();
        new OpenIddictFeatureProvider().DefineFeatures(catalog);

        catalog.Get(OpenIddictFeatures.Applications).Permission.ShouldBe(OpenIddictPermissions.Applications.Read);
        catalog.Get(OpenIddictFeatures.Scopes).Permission.ShouldBe(OpenIddictPermissions.Scopes.Read);
        catalog.Get(OpenIddictFeatures.Authorizations).Permission.ShouldBe(OpenIddictPermissions.Authorizations.Read);

        catalog.Get(OpenIddictFeatures.Applications).DefaultIcon.ShouldBe("app-window");
        catalog.Get(OpenIddictFeatures.Scopes).DefaultIcon.ShouldBe("scan");
        catalog.Get(OpenIddictFeatures.Authorizations).DefaultIcon.ShouldBe("badge-check");
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
