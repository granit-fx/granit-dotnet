using Granit.Identity.Federated.Privacy.DataExport;
using Granit.Identity.Federated.Privacy.Extensions;
using Granit.Privacy;
using Granit.Privacy.BlobStorage;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Privacy.Tests.Extensions;

public sealed class PrivacyBuilderIdentityFederatedExtensionsTests
{
    [Fact]
    public void AddGranitIdentityFederatedPrivacyProvider_RegistersProviderAsScoped()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        GranitPrivacyBuilder returned = builder.AddGranitIdentityFederatedPrivacyProvider();

        returned.ShouldBeSameAs(builder);
        ServiceDescriptor descriptor = services.Single(d => d.ServiceType == typeof(IdentityFederatedPrivacyDataProvider));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitIdentityFederatedPrivacyProvider_WiresBlobStorageStagingInfrastructure()
    {
        // The provider's ctor hard-depends on IStagedFragmentBuilder; the builder call must
        // register the Granit.Privacy.BlobStorage staging infra so a host does not need a
        // separate [DependsOn(GranitPrivacyBlobStorageModule)].
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        builder.AddGranitIdentityFederatedPrivacyProvider();

        services.ShouldContain(d => d.ServiceType == typeof(IStagedFragmentBuilder));
    }
}
