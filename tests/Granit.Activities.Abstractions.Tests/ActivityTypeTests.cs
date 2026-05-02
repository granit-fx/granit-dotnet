using Shouldly;
using Xunit;

namespace Granit.Activities.Abstractions.Tests;

public sealed class ActivityTypeTests
{
    [Fact]
    public void Equality_treats_records_with_identical_fields_as_equal()
    {
        ActivityType a = new("ToDo", "check-square", "Activity:ToDo");
        ActivityType b = new("ToDo", "check-square", "Activity:ToDo");
        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void Equality_distinguishes_default_duration()
    {
        ActivityType point = new("Call", "phone", "Activity:Call");
        ActivityType blocked = new("Call", "phone", "Activity:Call", DefaultDurationMinutes: 30);
        point.ShouldNotBe(blocked);
    }

    [Fact]
    public void DefaultDurationMinutes_is_optional()
    {
        ActivityType email = new("Email", "mail", "Activity:Email");
        email.DefaultDurationMinutes.ShouldBeNull();
    }
}
