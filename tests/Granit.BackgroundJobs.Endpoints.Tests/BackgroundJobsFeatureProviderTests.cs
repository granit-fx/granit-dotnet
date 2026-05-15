using Granit.BackgroundJobs.Endpoints.Permissions;
using Granit.BackgroundJobs.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Endpoints.Tests;

public sealed class BackgroundJobsFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new BackgroundJobsFeatureProvider().DefineFeatures(catalog);

        catalog.Get(BackgroundJobsFeatures.Jobs).Permission.ShouldBe(BackgroundJobsPermissions.Jobs.Read);
        catalog.Get(BackgroundJobsFeatures.Jobs).RouteName.ShouldBe(BackgroundJobsFeatures.Jobs);
        catalog.Get(BackgroundJobsFeatures.Jobs).DefaultIcon.ShouldBe("clock");
        catalog.Get(BackgroundJobsFeatures.Jobs).DisplayKey.ShouldBe("BackgroundJobsEndpoints:Workspace.Item");
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
