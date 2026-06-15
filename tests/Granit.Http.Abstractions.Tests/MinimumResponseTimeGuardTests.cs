using System.Diagnostics;
using Granit.Http.Timing;
using Shouldly;
using Xunit;

namespace Granit.Http.Abstractions.Tests;

/// <summary>
/// Validates <see cref="MinimumResponseTimeGuard"/> argument checks and timing-padding behavior.
/// </summary>
public sealed class MinimumResponseTimeGuardTests
{
    [Fact]
    public void Begin_with_negative_minimum_throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => MinimumResponseTimeGuard.Begin(-1, 10));

    [Fact]
    public void Begin_with_maximum_less_than_minimum_throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => MinimumResponseTimeGuard.Begin(10, 5));

    [Fact]
    public void Begin_with_equal_minimum_and_maximum_is_allowed()
    {
        var guard = MinimumResponseTimeGuard.Begin(0, 0);

        guard.ShouldBeOfType<MinimumResponseTimeGuard>();
    }

    [Fact]
    public async Task DisposeAsync_pads_to_floor_when_handler_is_fast()
    {
        const int floorMs = 200;

        long start = Stopwatch.GetTimestamp();
        await using (var guard = MinimumResponseTimeGuard.Begin(floorMs, floorMs))
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

        long start = Stopwatch.GetTimestamp();
        await using (var guard = MinimumResponseTimeGuard.Begin(floorMs, floorMs))
        {
            await Task.Delay(handlerWorkMs, TestContext.Current.CancellationToken);
        }
        TimeSpan elapsed = Stopwatch.GetElapsedTime(start);

        // The guard must not add extra padding once the floor is already reached.
        // Generous upper bound to keep the test robust on slow CI runners.
        elapsed.TotalMilliseconds.ShouldBeLessThan(handlerWorkMs + 80);
    }

    [Fact]
    public async Task Floor_picked_within_inclusive_range()
    {
        const int minMs = 30;
        const int maxMs = 60;

        // Run several iterations — each draws a fresh random floor in [minMs, maxMs].
        for (int i = 0; i < 5; i++)
        {
            long start = Stopwatch.GetTimestamp();
            await using (var guard = MinimumResponseTimeGuard.Begin(minMs, maxMs))
            {
                // No work.
            }
            TimeSpan elapsed = Stopwatch.GetElapsedTime(start);

            elapsed.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(minMs - 15);
            // Upper bound: floor cannot exceed maxMs, plus generous scheduler slack.
            elapsed.TotalMilliseconds.ShouldBeLessThan(maxMs + 150);
        }
    }
}
