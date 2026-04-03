using Granit.ReferenceData.Internal;
using Granit.ReferenceData.Options;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Tests.Internal;

public sealed class ReferenceDataRegistryInitializerTests
{
    [Fact]
    public async Task StartAsync_calls_all_contributors()
    {
        ReferenceDataRegistry registry = new();
        ReferenceDataTypeRegistration reg1 = new("Countries", new ReferenceDataExtensionOptions());
        ReferenceDataTypeRegistration reg2 = new("Currencies", new ReferenceDataExtensionOptions());

        IReferenceDataRegistryContributor[] contributors =
        [
            new ReferenceDataRegistryContributor(reg1),
            new ReferenceDataRegistryContributor(reg2),
        ];

        ReferenceDataRegistryInitializer initializer = new(registry, contributors);

        await initializer.StartAsync(CancellationToken.None);

        registry.Types.Count.ShouldBe(2);
        registry.TryGet("Countries", out _).ShouldBeTrue();
        registry.TryGet("Currencies", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task StartAsync_with_no_contributors_does_not_throw()
    {
        ReferenceDataRegistry registry = new();
        ReferenceDataRegistryInitializer initializer = new(registry, []);

        await initializer.StartAsync(CancellationToken.None);

        registry.Types.ShouldBeEmpty();
    }

    [Fact]
    public async Task StopAsync_completes_immediately()
    {
        ReferenceDataRegistry registry = new();
        ReferenceDataRegistryInitializer initializer = new(registry, []);

        Task result = initializer.StopAsync(CancellationToken.None);

        result.IsCompletedSuccessfully.ShouldBeTrue();
        await result;
    }
}
