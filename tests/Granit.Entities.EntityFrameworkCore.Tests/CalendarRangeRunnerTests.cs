using Granit.Domain;
using Granit.Entities.Endpoints;
using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.EntityFrameworkCore.Internal;
using Granit.Entities.Layouts;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Entities.EntityFrameworkCore.Tests;

public sealed class CalendarRangeRunnerTests
{
    private static readonly DateTimeOffset WindowFrom = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowTo = new(2026, 5, 7, 23, 59, 59, TimeSpan.Zero);

    public sealed class CalendarSampleEvent : Entity
    {
        public DateTimeOffset StartsAt { get; set; }
        public DateTimeOffset? EndsAt { get; set; }
        public string Subject { get; set; } = string.Empty;
    }

    private sealed class TestDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<CalendarSampleEvent> Events => Set<CalendarSampleEvent>();
    }

    private sealed class TestQueryableSource(TestDbContext context) : IQueryableSource<CalendarSampleEvent>
    {
        public IQueryable<CalendarSampleEvent> GetQueryable() => context.Events.AsQueryable();
    }

    private static (TestDbContext context, CalendarRangeRunner<CalendarSampleEvent> runner)
        CreateRunner(string dbName)
    {
        DbContextOptions options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        TestDbContext context = new(options);
        CalendarRangeRunner<CalendarSampleEvent> runner = new(new TestQueryableSource(context));
        return (context, runner);
    }

    [Fact]
    public async Task ExecuteAsync_FiltersEventsToWindow()
    {
        (TestDbContext context, CalendarRangeRunner<CalendarSampleEvent> runner) =
            CreateRunner(nameof(ExecuteAsync_FiltersEventsToWindow));

        context.Events.AddRange(
            new CalendarSampleEvent
            {
                Id = Guid.NewGuid(),
                StartsAt = WindowFrom.AddDays(2),
                EndsAt = WindowFrom.AddDays(2).AddHours(1),
                Subject = "Inside",
            },
            new CalendarSampleEvent
            {
                Id = Guid.NewGuid(),
                StartsAt = WindowFrom.AddDays(-30),
                EndsAt = WindowFrom.AddDays(-30).AddHours(1),
                Subject = "Before window",
            },
            new CalendarSampleEvent
            {
                Id = Guid.NewGuid(),
                StartsAt = WindowTo.AddDays(30),
                EndsAt = WindowTo.AddDays(30).AddHours(1),
                Subject = "After window",
            });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        CalendarLayoutDescriptor layout = new()
        {
            StartPropertyName = nameof(CalendarSampleEvent.StartsAt),
            Kind = EntityListLayoutKind.Calendar,
            EndPropertyName = nameof(CalendarSampleEvent.EndsAt),
            TitlePropertyName = nameof(CalendarSampleEvent.Subject),
            IsDefault = true,
        };

        IReadOnlyList<CalendarItemResponse> items = await runner.ExecuteAsync(
            layout,
            new CalendarRange(WindowFrom, WindowTo),
            displayProperty: null,
            TestContext.Current.CancellationToken);

        items.Count.ShouldBe(1);
        items[0].Title.ShouldBe("Inside");
    }

    [Fact]
    public async Task ExecuteAsync_PointInTime_RetainsEventsAtBoundaries()
    {
        (TestDbContext context, CalendarRangeRunner<CalendarSampleEvent> runner) =
            CreateRunner(nameof(ExecuteAsync_PointInTime_RetainsEventsAtBoundaries));

        context.Events.AddRange(
            new CalendarSampleEvent { Id = Guid.NewGuid(), StartsAt = WindowFrom, Subject = "On from" },
            new CalendarSampleEvent { Id = Guid.NewGuid(), StartsAt = WindowTo, Subject = "On to" });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        CalendarLayoutDescriptor layout = new()
        {
            StartPropertyName = nameof(CalendarSampleEvent.StartsAt),
            Kind = EntityListLayoutKind.Calendar,
            TitlePropertyName = nameof(CalendarSampleEvent.Subject),
            IsDefault = true,
        };

        IReadOnlyList<CalendarItemResponse> items = await runner.ExecuteAsync(
            layout,
            new CalendarRange(WindowFrom, WindowTo),
            displayProperty: null,
            TestContext.Current.CancellationToken);

        items.Count.ShouldBe(2);
    }
}
