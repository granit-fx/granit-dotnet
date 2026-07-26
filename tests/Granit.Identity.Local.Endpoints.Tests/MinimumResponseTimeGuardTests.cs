using System.Diagnostics;
using Granit.Identity.Local.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Endpoints.Tests;

/// <summary>
/// Validates <see cref="MinimumResponseTimeGuard"/> argument checks and timing-padding behavior.
/// </summary>
public sealed class MinimumResponseTimeGuardTests
{
    [Fact]
    public void Begin_with_negative_minimum_throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => MinimumResponseTimeGuard.Begin(-1, 10, TestContext.Current.CancellationToken));

    [Fact]
    public void Begin_with_maximum_less_than_minimum_throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => MinimumResponseTimeGuard.Begin(10, 5, TestContext.Current.CancellationToken));

    [Fact]
    public void Begin_with_equal_minimum_and_maximum_is_allowed()
    {
        var guard = MinimumResponseTimeGuard.Begin(0, 0, TestContext.Current.CancellationToken);

        guard.ShouldBeOfType<MinimumResponseTimeGuard>();
    }

    [Fact]
    public async Task DisposeAsync_pads_to_floor_when_handler_is_fast()
    {
        const int floorMs = 200;

        long start = Stopwatch.GetTimestamp();
        await using (var guard = MinimumResponseTimeGuard.Begin(floorMs, floorMs, TestContext.Current.CancellationToken))
        {
            // No work — should still take at least floorMs.
        }
        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);

        // Allow a small scheduler tolerance below the floor (Task.Delay can return ~15 ms early on busy CI).
        elapsed.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(floorMs - 20);
    }

    [Fact]
    public async Task DisposeAsync_is_noop_when_handler_already_exceeds_floor()
    {
        const int floorMs = 20;
        const int handlerWorkMs = 120;

        var guard = MinimumResponseTimeGuard.Begin(floorMs, floorMs, TestContext.Current.CancellationToken);
        await Task.Delay(handlerWorkMs, TestContext.Current.CancellationToken);

        // Time ONLY the dispose, not the total: the handler's Task.Delay can overshoot arbitrarily on
        // a loaded CI runner (that overshoot flaked the previous total-elapsed upper bound, see
        // Floor_picked_within_inclusive_range). Once the floor is already exceeded, dispose must add
        // no padding, so its own duration stays near zero regardless of how long the handler ran.
        long disposeStart = Stopwatch.GetTimestamp();
        await guard.DisposeAsync();
        TimeSpan disposeElapsed = Stopwatch.GetElapsedTime(disposeStart);

        disposeElapsed.TotalMilliseconds.ShouldBeLessThan(floorMs + 60);
    }

    [Fact]
    public async Task Floor_picked_within_inclusive_range()
    {
        const int minMs = 30;
        const int maxMs = 60;

        // Prove the randomised floor stays within [minMs, maxMs] by asserting the chosen
        // floor directly, not via wall-clock elapsed time: Task.Delay can overshoot
        // arbitrarily on a loaded CI runner, so an *upper* time bound flakes (that was the
        // original failure). Many draws exercise the RNG range with zero timing dependency.
        for (int i = 0; i < 1_000; i++)
        {
            // Not disposed: DisposeAsync is what applies the padding delay; here we only
            // inspect the floor chosen at Begin().
            var guard = MinimumResponseTimeGuard.Begin(minMs, maxMs, TestContext.Current.CancellationToken);

            guard.FloorMs.ShouldBeInRange(minMs, maxMs);
        }

        // Security-relevant: the padding delay must actually be applied on dispose so fast
        // failure paths cannot be timed. A *lower* bound never overshoots, so it is robust.
        long start = Stopwatch.GetTimestamp();
        await using (MinimumResponseTimeGuard.Begin(minMs, maxMs, TestContext.Current.CancellationToken))
        {
            // No work — dispose must still pad to at least the chosen floor (≥ minMs).
        }
        Stopwatch.GetElapsedTime(start).TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(minMs - 15);
    }
}
