using Granit.BlobStorage.Endpoints.Permissions;
using Granit.BlobStorage.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Endpoints.Tests;

public sealed class BlobStorageFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new BlobStorageFeatureProvider().DefineFeatures(catalog);

        catalog.Get(BlobStorageFeatures.Administration).Permission.ShouldBe(BlobStoragePermissions.Administration.Read);
        catalog.Get(BlobStorageFeatures.Administration).RouteName.ShouldBe(BlobStorageFeatures.Administration);
        catalog.Get(BlobStorageFeatures.Administration).DefaultIcon.ShouldBe("file");
        catalog.Get(BlobStorageFeatures.Administration).DisplayKey.ShouldBe("BlobStorageEndpoints:Workspace.Item");
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
