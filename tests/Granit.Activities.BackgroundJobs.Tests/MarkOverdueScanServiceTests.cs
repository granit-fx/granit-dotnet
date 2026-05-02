using Granit.Activities;
using Granit.Activities.Abstractions;
using Granit.Activities.BackgroundJobs.Services;
using Granit.Activities.Domain;
using Granit.Activities.Events;
using Granit.Events;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Activities.BackgroundJobs.Tests;

public sealed class MarkOverdueScanServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 10, 12, 0, 0, TimeSpan.Zero);

    private static IActivityRegistry RegistryWithToDo()
    {
        IActivityRegistry registry = Substitute.For<IActivityRegistry>();
        registry.TryGet("ToDo", out Arg.Any<ActivityType>()).Returns(call =>
        {
            call[1] = StandardActivityTypes.ToDo;
            return true;
        });
        return registry;
    }

    private static Activity OverdueActivity(DateTimeOffset due) =>
        Activity.Create(
            id: Guid.NewGuid(),
            entityType: "Granit.Parties.Party",
            entityId: Guid.NewGuid(),
            type: "ToDo",
            assignedToUserId: Guid.NewGuid(),
            dueAt: due,
            registry: RegistryWithToDo());

    private static (MarkOverdueScanService sut, IActivityReader reader, IActivityWriter writer, ILocalEventBus bus, IClock clock)
        BuildSut() =>
        BuildSut([]);

    private static (MarkOverdueScanService sut, IActivityReader reader, IActivityWriter writer, ILocalEventBus bus, IClock clock)
        BuildSut(IReadOnlyList<Activity> overdueRows)
    {
        IActivityReader reader = Substitute.For<IActivityReader>();
        reader.GetOverdueAwaitingNotificationAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(overdueRows);
        IActivityWriter writer = Substitute.For<IActivityWriter>();
        ILocalEventBus bus = Substitute.For<ILocalEventBus>();
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);
        clock.Normalize(Arg.Any<DateTimeOffset>()).Returns(call => call.Arg<DateTimeOffset>());
        return (new MarkOverdueScanService(reader, writer, bus, clock, NullLogger<MarkOverdueScanService>.Instance), reader, writer, bus, clock);
    }

    [Fact]
    public async Task ExecuteAsync_no_overdue_rows_no_publish_no_stamp()
    {
        (MarkOverdueScanService? sut, IActivityReader _, IActivityWriter? writer, ILocalEventBus? bus, IClock _) = BuildSut(overdueRows: []);
        await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        await bus.DidNotReceiveWithAnyArgs().PublishAsync<ActivityOverdueEvent>(default!, Arg.Any<CancellationToken>());
        await writer.DidNotReceiveWithAnyArgs().MarkOverdueNotifiedAsync(default, default, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_one_overdue_row_publishes_event_then_stamps()
    {
        Activity row = OverdueActivity(due: Now.AddDays(-2));
        (MarkOverdueScanService? sut, IActivityReader _, IActivityWriter? writer, ILocalEventBus? bus, IClock _) = BuildSut([row]);

        await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        await bus.Received(1).PublishAsync(
            Arg.Is<ActivityOverdueEvent>(e => e.ActivityId == row.Id && e.OverdueByDays >= 1),
            Arg.Any<CancellationToken>());
        await writer.Received(1).MarkOverdueNotifiedAsync(row.Id, Now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_overdueByDays_floor_is_one_for_sub_day_overdue()
    {
        Activity row = OverdueActivity(due: Now.AddHours(-3));   // 3h overdue → still 1 day floor
        (MarkOverdueScanService? sut, IActivityReader _, IActivityWriter _, ILocalEventBus? bus, IClock _) = BuildSut([row]);

        await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        await bus.Received(1).PublishAsync(
            Arg.Is<ActivityOverdueEvent>(e => e.OverdueByDays == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_uses_grace_cutoff_when_querying_reader()
    {
        (MarkOverdueScanService? sut, IActivityReader? reader, IActivityWriter _, ILocalEventBus _, IClock _) = BuildSut([]);
        await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        // Reader was called with `now - 1 hour` as the cutoff (grace period).
        await reader.Received(1).GetOverdueAwaitingNotificationAsync(
            Arg.Is<DateTimeOffset>(c => c == Now.AddHours(-1)),
            Arg.Any<CancellationToken>());
    }
}
