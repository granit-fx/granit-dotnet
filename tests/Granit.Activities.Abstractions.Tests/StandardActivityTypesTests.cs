using Shouldly;
using Xunit;

namespace Granit.Activities.Abstractions.Tests;

public sealed class StandardActivityTypesTests
{
    [Fact]
    public void All_exposes_the_four_starter_types_in_declaration_order()
    {
        StandardActivityTypes.All.Select(t => t.Name)
            .ShouldBe(["ToDo", "Call", "Meeting", "Email"]);
    }

    [Fact]
    public void Meeting_is_the_only_type_with_a_default_duration()
    {
        StandardActivityTypes.All.Count(t => t.DefaultDurationMinutes is not null).ShouldBe(1);
        StandardActivityTypes.Meeting.DefaultDurationMinutes.ShouldBe(30);
    }

    [Fact]
    public void DisplayKeys_follow_the_Activity_namespace_convention()
    {
        StandardActivityTypes.All.ShouldAllBe(t => t.DisplayKey.StartsWith("Activity:", StringComparison.Ordinal));
    }
}

public sealed class StandardActivityTypeProviderTests
{
    [Fact]
    public void Provide_returns_the_standard_catalog()
    {
        StandardActivityTypeProvider sut = new();
        sut.Provide().ShouldBe(StandardActivityTypes.All);
    }
}
