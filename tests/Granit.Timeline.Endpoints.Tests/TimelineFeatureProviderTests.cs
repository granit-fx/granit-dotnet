using Granit.Timeline.Endpoints.Permissions;
using Granit.Timeline.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Endpoints.Tests;

public sealed class TimelineFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new TimelineFeatureProvider().DefineFeatures(catalog);

        catalog.Get(TimelineFeatures.Entries).Permission.ShouldBe(TimelinePermissions.Entries.Read);
        catalog.Get(TimelineFeatures.Entries).RouteName.ShouldBe(TimelineFeatures.Entries);
        catalog.Get(TimelineFeatures.Entries).DefaultIcon.ShouldBe("activity");
        catalog.Get(TimelineFeatures.Entries).DisplayKey.ShouldBe("TimelineEndpoints:Workspace.Item");
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
