using Granit.Activities.Internal;
using Shouldly;
using Xunit;

namespace Granit.Activities.Tests.Internal;

public sealed class ActivityRegistryTests
{
    [Fact]
    public void All_aggregates_every_provider_contribution()
    {
        ActivityRegistry sut = new(
        [
            new StandardActivityTypeProvider(),
            new SalesProvider(),
        ]);

        sut.All.Count.ShouldBe(StandardActivityTypes.All.Count + 2);
        sut.All.ShouldContainKey("ToDo");
        sut.All.ShouldContainKey("Quote");
        sut.All.ShouldContainKey("Demo");
    }

    [Fact]
    public void TryGet_returns_false_for_unknown_type()
    {
        ActivityRegistry sut = new([new StandardActivityTypeProvider()]);
        sut.TryGet("UnknownType", out ActivityType? type).ShouldBeFalse();
        type.ShouldBeNull();
    }

    [Fact]
    public void TryGet_returns_true_with_matching_type_for_known_name()
    {
        ActivityRegistry sut = new([new StandardActivityTypeProvider()]);
        sut.TryGet("Meeting", out ActivityType? type).ShouldBeTrue();
        type!.DefaultDurationMinutes.ShouldBe(30);
    }

    [Fact]
    public void Constructor_throws_on_duplicate_type_name_across_providers()
    {
        Should.Throw<InvalidOperationException>(() => new ActivityRegistry(
        [
            new StandardActivityTypeProvider(),
            new DuplicateToDoProvider(),
        ]));
    }

    private sealed class SalesProvider : IActivityTypeProvider
    {
        public IEnumerable<ActivityType> Provide() =>
        [
            new("Quote", "file-text", "Activity:Quote"),
            new("Demo",  "monitor",   "Activity:Demo", DefaultDurationMinutes: 60),
        ];
    }

    private sealed class DuplicateToDoProvider : IActivityTypeProvider
    {
        public IEnumerable<ActivityType> Provide() =>
            [new("ToDo", "alert", "Activity:ToDo.Override")];
    }
}
