using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests;

public sealed class GroupedResultTests
{
    [Fact]
    public void Groups_And_TotalCount_Are_Preserved()
    {
        GroupEntry<string> group = new()
        {
            Field = "Status",
            Value = "Active",
            Label = "Active",
            Count = 5,
        };
        List<GroupEntry<string>> groups = [group];

        GroupedResult<string> result = new(groups, 5);

        result.Groups.ShouldBe(groups);
        result.TotalCount.ShouldBe(5);
    }

    [Fact]
    public void Empty_Grouped_Result()
    {
        GroupedResult<string> result = new([], 0);

        result.Groups.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public void Multiple_Groups_With_Aggregates()
    {
        GroupEntry<int> activeGroup = new()
        {
            Field = "Status",
            Value = "Active",
            Label = "Actif",
            Count = 10,
            Aggregates = new Dictionary<string, object?> { ["totalAmount"] = 1234.56m },
        };
        GroupEntry<int> inactiveGroup = new()
        {
            Field = "Status",
            Value = "Inactive",
            Label = "Inactif",
            Count = 3,
            Aggregates = new Dictionary<string, object?> { ["totalAmount"] = 100.00m },
        };

        GroupedResult<int> result = new([activeGroup, inactiveGroup], 13);

        result.Groups.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(13);
    }
}

public sealed class GroupEntryTests
{
    [Fact]
    public void Required_Properties_Are_Preserved()
    {
        GroupEntry<string> entry = new()
        {
            Field = "Country",
            Value = "BE",
            Label = "Belgium",
            Count = 42,
        };

        entry.Field.ShouldBe("Country");
        entry.Value.ShouldBe("BE");
        entry.Label.ShouldBe("Belgium");
        entry.Count.ShouldBe(42);
    }

    [Fact]
    public void Default_Aggregates_Is_Null()
    {
        GroupEntry<string> entry = new()
        {
            Field = "Status",
            Value = "Active",
            Label = "Active",
            Count = 1,
        };

        entry.Aggregates.ShouldBeNull();
    }

    [Fact]
    public void Default_Items_Is_Null()
    {
        GroupEntry<string> entry = new()
        {
            Field = "Status",
            Value = "Active",
            Label = "Active",
            Count = 1,
        };

        entry.Items.ShouldBeNull();
    }

    [Fact]
    public void Items_Can_Be_Populated_For_Drill_Down()
    {
        GroupEntry<string> entry = new()
        {
            Field = "Status",
            Value = "Active",
            Label = "Active",
            Count = 2,
            Items = ["Alice", "Bob"],
        };

        entry.Items.ShouldNotBeNull();
        entry.Items.Count.ShouldBe(2);
    }

    [Fact]
    public void Value_Can_Be_Null()
    {
        GroupEntry<string> entry = new()
        {
            Field = "Category",
            Value = null,
            Label = "(none)",
            Count = 3,
        };

        entry.Value.ShouldBeNull();
    }
}
