using Granit.Activities.Domain;
using Granit.Activities.Endpoints.Dtos;
using Granit.Activities.Endpoints.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Activities.Endpoints.Tests;

public sealed class ActivityResponseMapperTests
{
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

    [Fact]
    public void ToResponse_copies_every_aggregate_field()
    {
        var id = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var assignee = Guid.NewGuid();
        DateTimeOffset due = new(2026, 5, 10, 14, 0, 0, TimeSpan.Zero);

        var activity = Activity.Create(
            id, "Granit.Parties.Party", entityId, "ToDo", assignee, due,
            RegistryWithToDo(), createdByUserId: Guid.NewGuid(), description: "Call back");

        ActivityResponse response = activity.ToResponse();

        response.Id.ShouldBe(id);
        response.EntityType.ShouldBe("Granit.Parties.Party");
        response.EntityId.ShouldBe(entityId);
        response.Type.ShouldBe("ToDo");
        response.AssignedToUserId.ShouldBe(assignee);
        response.DueAt.ShouldBe(due);
        response.Description.ShouldBe("Call back");
        response.Status.ShouldBe(ActivityStatus.Open);
        response.CompletedAt.ShouldBeNull();
        response.CompletedByUserId.ShouldBeNull();
    }
}
