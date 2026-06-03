using Granit.Notifications.Privacy.DataExport;
using Granit.Notifications.Privacy.Extensions;
using Granit.Privacy;
using Granit.Privacy.BlobStorage;
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
        ServiceDescriptor descriptor = services.Single(d => d.ServiceType == typeof(NotificationsPrivacyDataProvider));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitNotificationsPrivacyProvider_WiresBlobStorageStagingInfrastructure()
    {
        // The provider's ctor hard-depends on IStagedFragmentBuilder; the builder call must
        // register the Granit.Privacy.BlobStorage staging infra so a host does not need a
        // separate [DependsOn(GranitPrivacyBlobStorageModule)].
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        builder.AddGranitNotificationsPrivacyProvider();

        services.ShouldContain(d => d.ServiceType == typeof(IStagedFragmentBuilder));
    }
}
