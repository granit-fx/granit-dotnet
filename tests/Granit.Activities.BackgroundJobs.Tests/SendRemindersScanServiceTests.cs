using Granit.Activities;
using Granit.Activities.BackgroundJobs.Services;
using Granit.Activities.Domain;
using Granit.Activities.Events;
using Granit.Activities.Persistence;
using Granit.Events;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.Activities.BackgroundJobs.Tests;

public sealed class SendRemindersScanServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 10, 8, 0, 0, TimeSpan.Zero);

    private static IActivityRegistry RegistryWithToDo()
    {
        IActivityRegistry registry = Substitute.For<IActivityRegistry>();
        registry.TryGet("ToDo", out Arg.Any<ActivityType?>()).Returns(call =>
        {
            call[1] = StandardActivityTypes.ToDo;
            return true;
        });
        return registry;
    }

    private static Activity DueActivity(DateTimeOffset due) =>
        Activity.Create(
            id: Guid.NewGuid(),
            entityType: "Granit.Parties.Party",
            entityId: Guid.NewGuid(),
            type: "ToDo",
            assignedToUserId: Guid.NewGuid(),
            dueAt: due,
            registry: RegistryWithToDo());

    private static (SendRemindersScanService sut, IActivityReader reader, ILocalEventBus bus)
        BuildSut(IReadOnlyList<Activity> dueRows)
    {
        IActivityReader reader = Substitute.For<IActivityReader>();
        reader.GetOpenDueWithinAsync(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(dueRows);
        ILocalEventBus bus = Substitute.For<ILocalEventBus>();
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);
        clock.Normalize(Arg.Any<DateTimeOffset>()).Returns(call => call.Arg<DateTimeOffset>());
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(_ => Substitute.For<IDisposable>());
        return (new SendRemindersScanService(reader, bus, tenant, clock, NullLogger<SendRemindersScanService>.Instance), reader, bus);
    }

    [Fact]
    public async Task ExecuteAsync_no_rows_no_publish()
    {
        (SendRemindersScanService? sut, IActivityReader _, ILocalEventBus? bus) = BuildSut(dueRows: []);
        await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        await bus.DidNotReceiveWithAnyArgs().PublishAsync<ActivityReminderDueEvent>(default!, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_publishes_one_event_per_due_row()
    {
        Activity[] rows = [DueActivity(Now.AddDays(1)), DueActivity(Now.AddDays(1).AddHours(6))];
        (SendRemindersScanService? sut, IActivityReader _, ILocalEventBus? bus) = BuildSut(rows);

        await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        await bus.Received(2).PublishAsync(
            Arg.Any<ActivityReminderDueEvent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_queries_reader_with_tomorrow_window()
    {
        (SendRemindersScanService? sut, IActivityReader? reader, ILocalEventBus _) = BuildSut(dueRows: []);
        await sut.ExecuteAsync(TestContext.Current.CancellationToken);

        // Now = 2026-05-10 08:00. Tomorrow = 2026-05-11 00:00. Day-after = 2026-05-12 00:00.
        DateTimeOffset expectedStart = new(new DateTime(2026, 5, 11), TimeSpan.Zero);
        DateTimeOffset expectedEnd = new(new DateTime(2026, 5, 12), TimeSpan.Zero);
        await reader.Received(1).GetOpenDueWithinAsync(
            Arg.Is<DateTimeOffset>(s => s == expectedStart),
            Arg.Is<DateTimeOffset>(e => e == expectedEnd),
            Arg.Any<CancellationToken>());
    }
}
