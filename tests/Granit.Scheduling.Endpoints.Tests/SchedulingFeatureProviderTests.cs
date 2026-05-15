using Granit.Scheduling.Endpoints.Permissions;
using Granit.Scheduling.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.Scheduling.Endpoints.Tests;

public sealed class SchedulingFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new SchedulingFeatureProvider().DefineFeatures(catalog);

        catalog.Get(SchedulingFeatures.Actions).Permission.ShouldBe(SchedulingPermissions.Actions.Read);
        catalog.Get(SchedulingFeatures.Actions).RouteName.ShouldBe(SchedulingFeatures.Actions);
        catalog.Get(SchedulingFeatures.Actions).DefaultIcon.ShouldBe("calendar-clock");
        catalog.Get(SchedulingFeatures.Actions).DisplayKey.ShouldBe("SchedulingEndpoints:Workspace.Item");
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
