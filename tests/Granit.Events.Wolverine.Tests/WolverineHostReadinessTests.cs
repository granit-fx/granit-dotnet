using Granit.Events.Wolverine.Internal;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Events.Wolverine.Tests;

public sealed class WolverineHostReadinessTests
{
    [Fact]
    public void IsReady_DefaultsFalse()
    {
        WolverineHostReadiness sut = new(NullLogger<WolverineHostReadiness>.Instance);

        sut.IsReady.ShouldBeFalse();
    }

    [Fact]
    public async Task IsReady_TrueAfterStartedAsync()
    {
        WolverineHostReadiness sut = new(NullLogger<WolverineHostReadiness>.Instance);
        IHostedLifecycleService lifecycle = sut;

        await lifecycle.StartedAsync(TestContext.Current.CancellationToken);

        sut.IsReady.ShouldBeTrue();
    }

    [Fact]
    public async Task IsReady_RemainingFalseAfterStartAsync()
    {
        WolverineHostReadiness sut = new(NullLogger<WolverineHostReadiness>.Instance);
        IHostedService hostedService = sut;

        await hostedService.StartAsync(TestContext.Current.CancellationToken);

        sut.IsReady.ShouldBeFalse();
    }

    [Fact]
    public async Task StopAsync_CompletesWithoutError()
    {
        WolverineHostReadiness sut = new(NullLogger<WolverineHostReadiness>.Instance);
        IHostedService hostedService = sut;

        await Should.NotThrowAsync(
            () => hostedService.StopAsync(TestContext.Current.CancellationToken));
    }
}
