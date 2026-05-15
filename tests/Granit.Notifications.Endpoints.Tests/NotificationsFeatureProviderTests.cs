using Granit.Notifications.Endpoints.Permissions;
using Granit.Notifications.Endpoints.Workspaces;
using Granit.Workspaces;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests;

public sealed class NotificationsFeatureProviderTests
{
    [Fact]
    public void DefineFeatures_registers_expected_features()
    {
        FakeCatalog catalog = new();
        new NotificationsFeatureProvider().DefineFeatures(catalog);

        catalog.Get(NotificationsFeatures.User).Permission.ShouldBe(NotificationPermissions.UserNotifications.Read);
        catalog.Get(NotificationsFeatures.User).RouteName.ShouldBe(NotificationsFeatures.User);
        catalog.Get(NotificationsFeatures.User).DefaultIcon.ShouldBe("bell");
        catalog.Get(NotificationsFeatures.User).DisplayKey.ShouldBe("NotificationsEndpoints:Workspace.Item");
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
