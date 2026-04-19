using Granit.Identity.Federated.Wolverine.DataExport;
using Granit.Identity.Federated.Wolverine.Extensions;
using Granit.Privacy;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Wolverine.Tests.Extensions;

public sealed class PrivacyBuilderIdentityFederatedExtensionsTests
{
    [Fact]
    public void AddGranitIdentityFederatedPrivacyProvider_RegistersProviderAsScoped()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        GranitPrivacyBuilder returned = builder.AddGranitIdentityFederatedPrivacyProvider();

        returned.ShouldBeSameAs(builder);
        ServiceDescriptor descriptor = services.ShouldHaveSingleItem();
        descriptor.ServiceType.ShouldBe(typeof(IdentityFederatedPrivacyDataProvider));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }
}
