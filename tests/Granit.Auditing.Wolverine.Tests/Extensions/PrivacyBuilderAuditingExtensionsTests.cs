using Granit.Auditing.Wolverine.DataExport;
using Granit.Auditing.Wolverine.Extensions;
using Granit.Privacy;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Wolverine.Tests.Extensions;

public sealed class PrivacyBuilderAuditingExtensionsTests
{
    [Fact]
    public void AddGranitAuditingPrivacyProvider_RegistersProviderAsScoped()
    {
        ServiceCollection services = new();
        GranitPrivacyBuilder builder = new(services);

        GranitPrivacyBuilder returned = builder.AddGranitAuditingPrivacyProvider();

        returned.ShouldBeSameAs(builder);
        ServiceDescriptor descriptor = services.ShouldHaveSingleItem();
        descriptor.ServiceType.ShouldBe(typeof(AuditingPrivacyDataProvider));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }
}
