using Granit.Auditing.Privacy.DataExport;
using Granit.Auditing.Privacy.Extensions;
using Granit.Privacy;
using Granit.Privacy.BlobStorage;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Privacy.Tests.Extensions;

public sealed class PrivacyBuilderAuditingExtensionsTests
{
    [Fact]
    public void AddGranitAuditingPrivacyProvider_RegistersProviderAsScoped()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        GranitPrivacyBuilder returned = builder.AddGranitAuditingPrivacyProvider();

        returned.ShouldBeSameAs(builder);
        ServiceDescriptor descriptor = services.Single(d => d.ServiceType == typeof(AuditingPrivacyDataProvider));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddGranitAuditingPrivacyProvider_WiresBlobStorageStagingInfrastructure()
    {
        // The provider's ctor hard-depends on IStagedFragmentBuilder; the builder call must
        // register the Granit.Privacy.BlobStorage staging infra so a host does not need a
        // separate [DependsOn(GranitPrivacyBlobStorageModule)].
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        builder.AddGranitAuditingPrivacyProvider();

        services.ShouldContain(d => d.ServiceType == typeof(IStagedFragmentBuilder));
    }
}
