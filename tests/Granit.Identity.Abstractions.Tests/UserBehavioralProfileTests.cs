using Shouldly;
using Xunit;

namespace Granit.Identity.Abstractions.Tests;

public sealed class UserBehavioralProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Recency = TimeSpan.FromDays(30);
    private static readonly TimeSpan Retention = TimeSpan.FromDays(180);
    private const int MinObservations = 3;

    private static UserBehavioralProfile Profile(int count, TimeSpan lastSeenAgo) =>
        new([new BehavioralObservation(
            BehavioralObservationKind.Country, "BE", count, Now - lastSeenAgo - TimeSpan.FromDays(1), Now - lastSeenAgo)]);

    [Fact]
    public void IsHabitual_FrequentWithinRetention_True() =>
        Profile(count: 5, lastSeenAgo: TimeSpan.FromDays(90))
            .IsHabitual(BehavioralObservationKind.Country, "BE", Now, MinObservations, Recency, Retention)
            .ShouldBeTrue();

    [Fact]
    public void IsHabitual_RecentButInfrequent_True() =>
        Profile(count: 1, lastSeenAgo: TimeSpan.FromDays(3))
            .IsHabitual(BehavioralObservationKind.Country, "BE", Now, MinObservations, Recency, Retention)
            .ShouldBeTrue();

    [Fact]
    public void IsHabitual_InfrequentAndOutsideRecency_False() =>
        Profile(count: 1, lastSeenAgo: TimeSpan.FromDays(60))
            .IsHabitual(BehavioralObservationKind.Country, "BE", Now, MinObservations, Recency, Retention)
            .ShouldBeFalse();

    [Fact]
    public void IsHabitual_FrequentButBeyondRetention_False() =>
        Profile(count: 10, lastSeenAgo: TimeSpan.FromDays(200))
            .IsHabitual(BehavioralObservationKind.Country, "BE", Now, MinObservations, Recency, Retention)
            .ShouldBeFalse();

    [Fact]
    public void IsHabitual_UnknownValue_False() =>
        Profile(count: 5, lastSeenAgo: TimeSpan.FromDays(1))
            .IsHabitual(BehavioralObservationKind.Country, "FR", Now, MinObservations, Recency, Retention)
            .ShouldBeFalse();

    [Fact]
    public void IsHabitual_WrongKind_False() =>
        Profile(count: 5, lastSeenAgo: TimeSpan.FromDays(1))
            .IsHabitual(BehavioralObservationKind.DeviceFamily, "BE", Now, MinObservations, Recency, Retention)
            .ShouldBeFalse();
}
