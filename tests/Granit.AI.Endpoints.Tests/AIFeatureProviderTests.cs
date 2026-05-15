using Granit.AI.Endpoints.Permissions;
using Granit.AI.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.AI.Endpoints.Tests;

public sealed class AIFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new AIFeatureProvider().DefineFeatures(catalog);

        catalog.Get(AIFeatures.Workspaces).Permission.ShouldBe(AIPermissions.Workspaces.Read);
        catalog.Get(AIFeatures.Workspaces).RouteName.ShouldBe(AIFeatures.Workspaces);
        catalog.Get(AIFeatures.Workspaces).DefaultIcon.ShouldBe("sparkles");
        catalog.Get(AIFeatures.Workspaces).DisplayKey.ShouldBe("AIEndpoints:Workspace.Workspaces");
        catalog.Get(AIFeatures.Usage).Permission.ShouldBe(AIPermissions.Usage.Read);
        catalog.Get(AIFeatures.Usage).RouteName.ShouldBe(AIFeatures.Usage);
        catalog.Get(AIFeatures.Usage).DefaultIcon.ShouldBe("chart-bar");
        catalog.Get(AIFeatures.Usage).DisplayKey.ShouldBe("AIEndpoints:Workspace.Usage");
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
