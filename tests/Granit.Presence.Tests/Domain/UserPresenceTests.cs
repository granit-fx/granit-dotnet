using Granit.Presence.Domain;
using Granit.Presence.Events;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Presence.Tests.Domain;

public sealed class UserPresenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 22, 12, 0, 0, TimeSpan.Zero);

    private static IClock CreateClock(DateTimeOffset? now = null)
    {
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(now ?? Now);
        return clock;
    }

    [Fact]
    public void Create_initializes_with_available_status_and_no_override()
    {
        var presence = UserPresence.Create(Guid.NewGuid(), CreateClock());

        presence.ManualStatus.ShouldBe(ManualPresenceStatus.Available);
        presence.OverrideUntilUtc.ShouldBeNull();
        presence.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Create_rejects_empty_user_id() =>
        Should.Throw<ArgumentException>(() => UserPresence.Create(Guid.Empty, CreateClock()));

    [Fact]
    public void SetOverride_records_status_and_until()
    {
        var presence = UserPresence.Create(Guid.NewGuid(), CreateClock());
        DateTimeOffset until = Now.AddHours(2);

        presence.SetOverride(ManualPresenceStatus.DoNotDisturb, until, CreateClock());

        presence.ManualStatus.ShouldBe(ManualPresenceStatus.DoNotDisturb);
        presence.OverrideUntilUtc.ShouldBe(until);
    }

    [Fact]
    public void SetOverride_with_available_clears_existing_override()
    {
        var presence = UserPresence.Create(Guid.NewGuid(), CreateClock());
        presence.SetOverride(ManualPresenceStatus.Busy, Now.AddHours(1), CreateClock());
        presence.ClearDomainEvents();

        presence.SetOverride(ManualPresenceStatus.Available, null, CreateClock());

        presence.ManualStatus.ShouldBe(ManualPresenceStatus.Available);
        presence.OverrideUntilUtc.ShouldBeNull();
        presence.DomainEvents
            .OfType<UserPresenceOverrideChangedEvent>()
            .ShouldContain(e => e.ToStatus == ManualPresenceStatus.Available);
    }

    [Fact]
    public void SetOverride_raises_event_only_on_transition()
    {
        var presence = UserPresence.Create(Guid.NewGuid(), CreateClock());
        presence.SetOverride(ManualPresenceStatus.Busy, null, CreateClock());
        presence.ClearDomainEvents();

        presence.SetOverride(ManualPresenceStatus.Busy, null, CreateClock());

        presence.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void IsOverrideExpired_returns_true_when_until_in_past()
    {
        var presence = UserPresence.Create(Guid.NewGuid(), CreateClock());
        presence.SetOverride(ManualPresenceStatus.DoNotDisturb, Now.AddMinutes(-5), CreateClock());

        presence.IsOverrideExpired(CreateClock()).ShouldBeTrue();
    }

    [Fact]
    public void IsOverrideExpired_returns_false_when_until_in_future()
    {
        var presence = UserPresence.Create(Guid.NewGuid(), CreateClock());
        presence.SetOverride(ManualPresenceStatus.DoNotDisturb, Now.AddMinutes(5), CreateClock());

        presence.IsOverrideExpired(CreateClock()).ShouldBeFalse();
    }

    [Fact]
    public void IsOverrideExpired_returns_false_when_until_is_null()
    {
        var presence = UserPresence.Create(Guid.NewGuid(), CreateClock());
        presence.SetOverride(ManualPresenceStatus.DoNotDisturb, null, CreateClock());

        presence.IsOverrideExpired(CreateClock()).ShouldBeFalse();
    }

    [Fact]
    public void GetActiveOverride_returns_null_for_available()
    {
        var presence = UserPresence.Create(Guid.NewGuid(), CreateClock());

        presence.GetActiveOverride(CreateClock()).ShouldBeNull();
    }

    [Fact]
    public void GetActiveOverride_returns_null_for_expired_override()
    {
        var presence = UserPresence.Create(Guid.NewGuid(), CreateClock());
        presence.SetOverride(ManualPresenceStatus.Busy, Now.AddMinutes(-1), CreateClock());

        presence.GetActiveOverride(CreateClock()).ShouldBeNull();
    }
}
