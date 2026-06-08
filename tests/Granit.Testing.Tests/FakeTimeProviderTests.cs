using Granit.Testing.Fakes;
using Shouldly;

namespace Granit.Testing.Tests;

public sealed class FakeTimeProviderTests
{
    private static readonly DateTimeOffset DefaultNow = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Default_Constructor_Returns_DefaultNow()
    {
        FakeTimeProvider sut = new();

        sut.GetUtcNow().ShouldBe(DefaultNow);
    }

    [Fact]
    public void Constructor_With_DateTimeOffset_Pins_Time()
    {
        DateTimeOffset pinned = new(2024, 6, 1, 8, 0, 0, TimeSpan.Zero);
        FakeTimeProvider sut = new(pinned);

        sut.GetUtcNow().ShouldBe(pinned);
    }

    [Fact]
    public void SetUtcNow_Updates_Time()
    {
        FakeTimeProvider sut = new();
        DateTimeOffset newTime = new(2025, 12, 31, 23, 59, 59, TimeSpan.Zero);

        sut.SetUtcNow(newTime);

        sut.GetUtcNow().ShouldBe(newTime);
    }

    [Fact]
    public void Advance_Increments_Time()
    {
        DateTimeOffset start = new(2026, 5, 12, 10, 0, 0, TimeSpan.Zero);
        FakeTimeProvider sut = new(start);

        sut.Advance(TimeSpan.FromHours(2));

        sut.GetUtcNow().ShouldBe(start.AddHours(2));
    }

    [Fact]
    public void Advance_Multiple_Times_Accumulates()
    {
        DateTimeOffset start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        FakeTimeProvider sut = new(start);

        sut.Advance(TimeSpan.FromDays(1));
        sut.Advance(TimeSpan.FromHours(3));

        sut.GetUtcNow().ShouldBe(start.AddDays(1).AddHours(3));
    }

    [Fact]
    public void Separate_Instances_Are_Independent()
    {
        FakeTimeProvider sut1 = new();
        FakeTimeProvider sut2 = new();

        sut1.Advance(TimeSpan.FromDays(30));

        sut2.GetUtcNow().ShouldBe(DefaultNow, "sut2 must not be affected by sut1 mutations");
    }

    [Fact]
    public async Task Time_PersistsAcross_Awaits()
    {
        // AsyncLocal loses its value when an async continuation resumes in a sibling
        // execution context (e.g. xUnit v3 IAsyncLifetime.InitializeAsync vs test method).
        // Instance fields survive across all contexts — this test documents that contract.
        DateTimeOffset pinned = new(2026, 5, 12, 10, 0, 0, TimeSpan.Zero);
        FakeTimeProvider sut = new(pinned);

        await Task.Delay(1, TestContext.Current.CancellationToken);
        DateTimeOffset after = sut.GetUtcNow();

        after.ShouldBe(pinned, "time must be unchanged after await — not reset to default");
    }

    [Fact]
    public async Task SetUtcNow_PersistsAcross_Awaits()
    {
        FakeTimeProvider sut = new();
        DateTimeOffset updated = new(2026, 5, 12, 10, 0, 0, TimeSpan.Zero);

        sut.SetUtcNow(updated);
        await Task.Delay(1, TestContext.Current.CancellationToken);

        sut.GetUtcNow().ShouldBe(updated, "SetUtcNow must persist after await continuation");
    }
}
