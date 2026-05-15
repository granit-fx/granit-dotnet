using Granit.Workflow.Endpoints.Permissions;
using Granit.Workflow.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Endpoints.Tests;

public sealed class WorkflowFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new WorkflowFeatureProvider().DefineFeatures(catalog);

        catalog.Get(WorkflowFeatures.History).Permission.ShouldBe(WorkflowPermissions.History.Read);
        catalog.Get(WorkflowFeatures.History).RouteName.ShouldBe(WorkflowFeatures.History);
        catalog.Get(WorkflowFeatures.History).DefaultIcon.ShouldBe("git-branch");
        catalog.Get(WorkflowFeatures.History).DisplayKey.ShouldBe("WorkflowEndpoints:Workspace.History");
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
