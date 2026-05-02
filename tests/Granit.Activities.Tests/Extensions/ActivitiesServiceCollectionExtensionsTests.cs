using Granit.Activities.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Activities.Tests.Extensions;

public sealed class ActivitiesServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitActivities_registers_registry_and_standard_provider()
    {
        ServiceCollection services = [];
        services.AddGranitActivities();

        services.Single(sd => sd.ServiceType == typeof(IActivityRegistry))
            .Lifetime.ShouldBe(ServiceLifetime.Singleton);
        services.Single(sd => sd.ServiceType == typeof(IActivityTypeProvider))
            .ImplementationType.ShouldBe(typeof(StandardActivityTypeProvider));
    }
}
