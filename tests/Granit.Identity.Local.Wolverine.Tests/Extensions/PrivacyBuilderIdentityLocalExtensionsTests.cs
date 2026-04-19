using Granit.Identity.Local.Wolverine.DataExport;
using Granit.Identity.Local.Wolverine.Extensions;
using Granit.Privacy;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Wolverine.Tests.Extensions;

public sealed class PrivacyBuilderIdentityLocalExtensionsTests
{
    [Fact]
    public void AddGranitIdentityLocalPrivacyProvider_RegistersProviderAsScoped()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        GranitPrivacyBuilder returned = builder.AddGranitIdentityLocalPrivacyProvider();

        returned.ShouldBeSameAs(builder);
        ServiceDescriptor descriptor = services.ShouldHaveSingleItem();
        descriptor.ServiceType.ShouldBe(typeof(IdentityLocalPrivacyDataProvider));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }
}
