using Granit.Activities.Domain;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Activities.EntityFrameworkCore.Tests;

public sealed class MarkOverdueNotifiedTests
{
    private static IActivityRegistry RegistryWithToDo()
    {
        IActivityRegistry registry = Substitute.For<IActivityRegistry>();
        ActivityType type = StandardActivityTypes.ToDo;
        registry.TryGet("ToDo", out Arg.Any<ActivityType?>()).Returns(call =>
        {
            call[1] = type;
            return true;
        });
        return registry;
    }

    [Fact]
    public void MarkOverdueNotified_records_first_fire_timestamp()
    {
        var activity = Activity.Create(Guid.NewGuid(), "Granit.Parties.Party",
            Guid.NewGuid(), "ToDo", Guid.NewGuid(), DateTimeOffset.UtcNow, RegistryWithToDo());

        DateTimeOffset firedAt = new(2026, 5, 3, 8, 0, 0, TimeSpan.Zero);
        activity.MarkOverdueNotified(firedAt);

        activity.OverdueNotifiedAt.ShouldBe(firedAt);
    }

    [Fact]
    public void MarkOverdueNotified_is_idempotent_subsequent_calls_preserve_first_timestamp()
    {
        var activity = Activity.Create(Guid.NewGuid(), "Granit.Parties.Party",
            Guid.NewGuid(), "ToDo", Guid.NewGuid(), DateTimeOffset.UtcNow, RegistryWithToDo());

        DateTimeOffset first = new(2026, 5, 3, 8, 0, 0, TimeSpan.Zero);
        DateTimeOffset second = first.AddHours(2);

        activity.MarkOverdueNotified(first);
        activity.MarkOverdueNotified(second);

        activity.OverdueNotifiedAt.ShouldBe(first);
    }
}
