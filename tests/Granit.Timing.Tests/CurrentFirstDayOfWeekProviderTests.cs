// =============================================================================
// Tests - CurrentFirstDayOfWeekProvider
// =============================================================================
// Verifies the AsyncLocal-backed first-day-of-week provider: default is null
// ("derive from culture"), stores an explicit override, and isolates the value
// per async execution context.
// =============================================================================

using Shouldly;
using Xunit;

namespace Granit.Timing.Tests;

public sealed class CurrentFirstDayOfWeekProviderTests
{
    [Fact]
    public void FirstDayOfWeek_defaults_to_null()
    {
        var provider = new CurrentFirstDayOfWeekProvider();

        provider.FirstDayOfWeek.ShouldBeNull();
    }

    [Fact]
    public void FirstDayOfWeek_stores_the_configured_value()
    {
        var provider = new CurrentFirstDayOfWeekProvider();

        provider.FirstDayOfWeek = DayOfWeek.Monday;

        provider.FirstDayOfWeek.ShouldBe(DayOfWeek.Monday);
    }

    [Fact]
    public async Task FirstDayOfWeek_is_isolated_per_async_context()
    {
        var provider = new CurrentFirstDayOfWeekProvider();
        provider.FirstDayOfWeek = DayOfWeek.Monday;

        DayOfWeek? observed = DayOfWeek.Sunday;
        await Task.Run(
            () =>
            {
                provider.FirstDayOfWeek = DayOfWeek.Sunday; // mutate inside a nested flow
                observed = provider.FirstDayOfWeek;
            },
            TestContext.Current.CancellationToken);

        observed.ShouldBe(DayOfWeek.Sunday);
        // The outer context keeps its own value (AsyncLocal does not propagate back up).
        provider.FirstDayOfWeek.ShouldBe(DayOfWeek.Monday);
    }
}
