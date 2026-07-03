using Granit.Testing.Fakes;
using Shouldly;

namespace Granit.Testing.Tests;

public sealed class FakeClockTests
{
    private static readonly DateTimeOffset DefaultNow = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Default_Now_Is_Fixed_Date()
    {
        FakeClock clock = new();

        clock.Now.ShouldBe(DefaultNow);
    }

    [Fact]
    public void Now_Can_Be_Set()
    {
        FakeClock clock = new();
        DateTimeOffset custom = new(2030, 6, 1, 12, 0, 0, TimeSpan.Zero);

        clock.Now = custom;

        clock.Now.ShouldBe(custom);
    }

    [Fact]
    public void Advance_Moves_Clock_Forward()
    {
        FakeClock clock = new();

        clock.Advance(TimeSpan.FromHours(2));

        clock.Now.ShouldBe(DefaultNow.AddHours(2));
    }

    [Fact]
    public void Advance_Accumulates()
    {
        FakeClock clock = new();

        clock.Advance(TimeSpan.FromMinutes(30));
        clock.Advance(TimeSpan.FromMinutes(30));

        clock.Now.ShouldBe(DefaultNow.AddHours(1));
    }

    [Fact]
    public void SupportsMultipleTimezone_Returns_False()
    {
        FakeClock clock = new();

        clock.SupportsMultipleTimezone.ShouldBeFalse();
    }

    [Fact]
    public void Normalize_Converts_To_Utc()
    {
        FakeClock clock = new();
        DateTimeOffset local = new(2026, 6, 15, 14, 0, 0, TimeSpan.FromHours(2));

        DateTimeOffset result = clock.Normalize(local);

        result.Offset.ShouldBe(TimeSpan.Zero);
        result.Hour.ShouldBe(12);
    }

    [Fact]
    public void ConvertToUserTime_Returns_Input_Unchanged()
    {
        FakeClock clock = new();
        DateTimeOffset utc = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        clock.ConvertToUserTime(utc).ShouldBe(utc);
    }

    [Fact]
    public void ConvertToUtc_Converts_To_Utc()
    {
        FakeClock clock = new();
        DateTimeOffset local = new(2026, 6, 15, 14, 0, 0, TimeSpan.FromHours(2));

        DateTimeOffset result = clock.ConvertToUtc(local);

        result.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void ResolveUserTimeZone_Returns_Utc()
    {
        FakeClock clock = new();

        clock.ResolveUserTimeZone().ShouldBe(TimeZoneInfo.Utc);
    }

    [Fact]
    public void ToUtcFromUserLocal_Treats_WallClock_As_Utc()
    {
        FakeClock clock = new();

        DateTimeOffset result = clock.ToUtcFromUserLocal(new DateTime(2026, 6, 15, 14, 0, 0));

        result.ShouldBe(new DateTimeOffset(2026, 6, 15, 14, 0, 0, TimeSpan.Zero));
        result.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public async Task AsyncLocal_Isolates_State_Across_Tasks()
    {
        FakeClock clock = new();

#pragma warning disable xUnit1051
        var task1 = Task.Run(async () =>
        {
            clock.Now = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            await Task.Delay(50);
            clock.Now.Year.ShouldBe(2020);
        });

        var task2 = Task.Run(async () =>
        {
            clock.Now = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
            await Task.Delay(50);
            clock.Now.Year.ShouldBe(2030);
        });

        await Task.WhenAll(task1, task2);
#pragma warning restore xUnit1051
    }
}
