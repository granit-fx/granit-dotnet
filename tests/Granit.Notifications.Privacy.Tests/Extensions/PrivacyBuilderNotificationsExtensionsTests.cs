using Granit.Notifications.Privacy.DataExport;
using Granit.Notifications.Privacy.Extensions;
using Granit.Privacy;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Privacy.Tests.Extensions;

public sealed class PrivacyBuilderNotificationsExtensionsTests
{
    [Fact]
    public void AddGranitNotificationsPrivacyProvider_RegistersProviderAsScoped()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        GranitPrivacyBuilder returned = builder.AddGranitNotificationsPrivacyProvider();

        returned.ShouldBeSameAs(builder);
        ServiceDescriptor descriptor = services.ShouldHaveSingleItem();
        descriptor.ServiceType.ShouldBe(typeof(NotificationsPrivacyDataProvider));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }
}
