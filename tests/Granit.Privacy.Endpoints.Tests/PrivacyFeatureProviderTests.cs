using Granit.Privacy.Endpoints.Permissions;
using Granit.Privacy.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests;

public sealed class PrivacyFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new PrivacyFeatureProvider().DefineFeatures(catalog);

        catalog.Get(PrivacyFeatures.Purposes).Permission.ShouldBe(PrivacyPermissions.Purposes.Read);
        catalog.Get(PrivacyFeatures.Purposes).RouteName.ShouldBe(PrivacyFeatures.Purposes);
        catalog.Get(PrivacyFeatures.Purposes).DefaultIcon.ShouldBe("shield-check");
        catalog.Get(PrivacyFeatures.Purposes).DisplayKey.ShouldBe("PrivacyEndpoints:Workspace.Purposes");
        catalog.Get(PrivacyFeatures.Agreements).Permission.ShouldBe(PrivacyPermissions.Agreements.Read);
        catalog.Get(PrivacyFeatures.Agreements).RouteName.ShouldBe(PrivacyFeatures.Agreements);
        catalog.Get(PrivacyFeatures.Agreements).DefaultIcon.ShouldBe("file-signature");
        catalog.Get(PrivacyFeatures.Agreements).DisplayKey.ShouldBe("PrivacyEndpoints:Workspace.Agreements");
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
