using Granit.Auditing.Endpoints.Permissions;
using Granit.Auditing.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Endpoints.Tests;

public sealed class AuditingFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new AuditingFeatureProvider().DefineFeatures(catalog);

        catalog.Get(AuditingFeatures.Entries).Permission.ShouldBe(AuditingPermissions.AuditEntries.Read);
        catalog.Get(AuditingFeatures.Entries).RouteName.ShouldBe(AuditingFeatures.Entries);
        catalog.Get(AuditingFeatures.Entries).DefaultIcon.ShouldBe("clipboard-list");
        catalog.Get(AuditingFeatures.Entries).DisplayKey.ShouldBe("AuditingEndpoints:Workspace.Item");
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
