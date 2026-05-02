using Granit.Activities.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Activities.Abstractions.Tests;

public sealed class ActivityTypeServiceCollectionExtensionsTests
{
    [Fact]
    public void AddActivityTypeProvider_registers_as_singleton_IActivityTypeProvider()
    {
        ServiceCollection services = [];
        services.AddActivityTypeProvider<StandardActivityTypeProvider>();

        ServiceDescriptor descriptor = services.Single(sd => sd.ServiceType == typeof(IActivityTypeProvider));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        descriptor.ImplementationType.ShouldBe(typeof(StandardActivityTypeProvider));
    }

    [Fact]
    public void AddActivityTypeProvider_supports_multiple_providers_per_host()
    {
        ServiceCollection services = [];
        services.AddActivityTypeProvider<StandardActivityTypeProvider>();
        services.AddActivityTypeProvider<FakeProvider>();

        // Multiple registrations under the same service type — the runtime
        // module (story A2) injects IEnumerable<IActivityTypeProvider> and
        // aggregates them into the registry.
        services.Count(sd => sd.ServiceType == typeof(IActivityTypeProvider)).ShouldBe(2);
    }

    private sealed class FakeProvider : IActivityTypeProvider
    {
        public IEnumerable<ActivityType> Provide() =>
            [new("Quote", "file-text", "Activity:Quote")];
    }
}
