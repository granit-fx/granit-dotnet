using Granit.Entities.Endpoints;
using Granit.Entities.Endpoints.Dtos;
using Granit.Entities.EntityFrameworkCore.Internal;
using Granit.Entities.Layouts;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Entities.EntityFrameworkCore.Tests;

public sealed class EntityFrameworkCoreCalendarRangeServiceTests
{
    [Fact]
    public async Task GetItemsAsync_DispatchesToMatchingRunner()
    {
        ICalendarRangeRunner runner = Substitute.For<ICalendarRangeRunner>();
        runner.EntityName.Returns("Party");

        IReadOnlyList<CalendarItemResponse> expected =
        [
            new CalendarItemResponse(Guid.NewGuid(), DateTimeOffset.UtcNow, null, "X", null),
        ];
        runner.ExecuteAsync(
                Arg.Any<CalendarLayoutDescriptor>(),
                Arg.Any<CalendarRange>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(expected);

        EntityFrameworkCoreCalendarRangeService sut = new([runner]);

        EntityDefinitionDescriptor descriptor = new()
        {
            Name = "Granit.Parties.Party",
            EntityType = typeof(Party),
            PermissionGroup = "Parties",
            Forms = [],
            Details = [],
            MetricDefinitionTypes = [],
            DashboardDefinitionTypes = [],
        };

        IReadOnlyList<CalendarItemResponse> result = await sut.GetItemsAsync(
            descriptor,
            new CalendarLayoutDescriptor { StartPropertyName = "X", Kind = EntityListLayoutKind.Calendar, IsDefault = true },
            new CalendarRange(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)),
            TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task GetItemsAsync_NoMatchingRunner_ReturnsEmptyList()
    {
        EntityFrameworkCoreCalendarRangeService sut = new([]);

        EntityDefinitionDescriptor descriptor = new()
        {
            Name = "Granit.Parties.Party",
            EntityType = typeof(Party),
            PermissionGroup = "Parties",
            Forms = [],
            Details = [],
            MetricDefinitionTypes = [],
            DashboardDefinitionTypes = [],
        };

        IReadOnlyList<CalendarItemResponse> result = await sut.GetItemsAsync(
            descriptor,
            new CalendarLayoutDescriptor { StartPropertyName = "X", Kind = EntityListLayoutKind.Calendar, IsDefault = true },
            new CalendarRange(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)),
            TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    private sealed class Party { }
}
