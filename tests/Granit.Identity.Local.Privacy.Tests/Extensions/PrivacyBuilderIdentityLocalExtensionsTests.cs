using Granit.Identity.Local.Privacy.DataExport;
using Granit.Identity.Local.Privacy.Extensions;
using Granit.Privacy;
using Granit.Privacy.BlobStorage;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Privacy.Tests.Extensions;

public sealed class PrivacyBuilderIdentityLocalExtensionsTests
{
    [Fact]
    public void AddGranitIdentityLocalPrivacyProvider_RegistersProviderAsScoped()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        GranitPrivacyBuilder returned = builder.AddGranitIdentityLocalPrivacyProvider();

        returned.ShouldBeSameAs(builder);
        ServiceDescriptor descriptor = services.Single(d => d.ServiceType == typeof(IdentityLocalPrivacyDataProvider));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitIdentityLocalPrivacyProvider_WiresBlobStorageStagingInfrastructure()
    {
        // The provider's ctor hard-depends on IStagedFragmentBuilder; the builder call must
        // register the Granit.Privacy.BlobStorage staging infra so a host does not need a
        // separate [DependsOn(GranitPrivacyBlobStorageModule)].
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        builder.AddGranitIdentityLocalPrivacyProvider();

        services.ShouldContain(d => d.ServiceType == typeof(IStagedFragmentBuilder));
    }
}
