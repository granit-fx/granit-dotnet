using Granit.Templating.Endpoints.Permissions;
using Granit.Templating.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.Templating.Endpoints.Tests;

public sealed class TemplatingFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new TemplatingFeatureProvider().DefineFeatures(catalog);

        catalog.Get(TemplatingFeatures.Templates).Permission.ShouldBe(TemplatingPermissions.Templates.Read);
        catalog.Get(TemplatingFeatures.Templates).RouteName.ShouldBe(TemplatingFeatures.Templates);
        catalog.Get(TemplatingFeatures.Templates).DefaultIcon.ShouldBe("layout-template");
        catalog.Get(TemplatingFeatures.Templates).DisplayKey.ShouldBe("TemplatingEndpoints:Workspace.Templates");
        catalog.Get(TemplatingFeatures.Categories).Permission.ShouldBe(TemplatingPermissions.Categories.Read);
        catalog.Get(TemplatingFeatures.Categories).RouteName.ShouldBe(TemplatingFeatures.Categories);
        catalog.Get(TemplatingFeatures.Categories).DefaultIcon.ShouldBe("folder-tree");
        catalog.Get(TemplatingFeatures.Categories).DisplayKey.ShouldBe("TemplatingEndpoints:Workspace.Categories");
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
