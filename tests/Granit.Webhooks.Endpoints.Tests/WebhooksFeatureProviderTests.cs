using Granit.Webhooks.Endpoints.Permissions;
using Granit.Webhooks.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Endpoints.Tests;

public sealed class WebhooksFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new WebhooksFeatureProvider().DefineFeatures(catalog);

        catalog.Get(WebhooksFeatures.Subscriptions).Permission.ShouldBe(WebhooksPermissions.Subscriptions.Read);
        catalog.Get(WebhooksFeatures.Subscriptions).RouteName.ShouldBe(WebhooksFeatures.Subscriptions);
        catalog.Get(WebhooksFeatures.Subscriptions).DefaultIcon.ShouldBe("webhook");
        catalog.Get(WebhooksFeatures.Subscriptions).DisplayKey.ShouldBe("WebhooksEndpoints:Workspace.Item");
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
