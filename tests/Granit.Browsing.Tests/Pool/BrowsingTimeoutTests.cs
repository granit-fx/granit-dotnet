using Granit.Browsing.Pool;
using Shouldly;
using Xunit;

namespace Granit.Browsing.Tests.Pool;

public sealed class BrowsingTimeoutTests
{
    [Fact]
    public async Task Should_throw_TimeoutException_when_operation_exceeds_cap()
    {
        var cap = TimeSpan.FromMilliseconds(50);

        TimeoutException? ex = await Should.ThrowAsync<TimeoutException>(async () =>
            await BrowsingTimeout.RunAsync(
                async ct => { await Task.Delay(TimeSpan.FromSeconds(5), ct); return 1; },
                cap,
                CancellationToken.None));

        ex.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_complete_when_operation_finishes_within_cap()
    {
        int result = await BrowsingTimeout.RunAsync(
            ct => Task.FromResult(42),
            TimeSpan.FromSeconds(1),
            CancellationToken.None);

        result.ShouldBe(42);
    }

    [Fact]
    public async Task Null_cap_should_run_unbounded()
    {
        int result = await BrowsingTimeout.RunAsync<int>(
            async ct => { await Task.Delay(10, ct); return 7; },
            null,
            CancellationToken.None);

        result.ShouldBe(7);
    }

    [Fact]
    public async Task Outer_cancellation_should_propagate()
    {
        using CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromMilliseconds(20));

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await BrowsingTimeout.RunAsync<int>(
                async ct => { await Task.Delay(TimeSpan.FromSeconds(5), ct); return 1; },
                TimeSpan.FromSeconds(10),
                cts.Token));
    }
}
