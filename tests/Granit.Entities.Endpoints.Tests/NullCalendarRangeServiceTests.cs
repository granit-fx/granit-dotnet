using Granit.Entities;
using Granit.Entities.Internal;
using Granit.Entities.Layouts;
using Shouldly;
using Xunit;

namespace Granit.Entities.Endpoints.Tests;

public sealed class NullCalendarRangeServiceTests
{
    [Fact]
    public async Task Default_implementation_returns_an_empty_list()
    {
        NullCalendarRangeService sut = new();

        EntityDefinitionDescriptor descriptor = new()
        {
            Name = "Sample",
            EntityType = typeof(object),
            Forms = [],
            Details = [],
            Relations = [],
            ListLayouts = [],
            Actions = [],
            MetricDefinitionTypes = [],
            DashboardDefinitionTypes = [],
        };
        CalendarLayoutDescriptor layout = new()
        {
            Kind = EntityListLayoutKind.Calendar,
            StartPropertyName = "StartsAt",
        };

        IReadOnlyList<CalendarItemResponse> items = await sut.GetItemsAsync(
            descriptor,
            layout,
            new CalendarRange(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(7)),
            TestContext.Current.CancellationToken);

        items.ShouldBeEmpty();
    }
}
