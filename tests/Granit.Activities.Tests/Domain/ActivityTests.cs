using Granit.Activities.Domain;
using Granit.Activities.Events;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Activities.Tests.Domain;

public sealed class ActivityTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 2, 12, 0, 0, TimeSpan.Zero);
    private const string SampleEntityType = "Granit.Parties.Party";
    private static readonly Guid SampleEntityId = Guid.NewGuid();
    private static readonly Guid SampleAssignee = Guid.NewGuid();
    private static readonly Guid SampleCreator = Guid.NewGuid();

    private static IActivityRegistry RegistryWithToDo()
    {
        IActivityRegistry registry = Substitute.For<IActivityRegistry>();
        ActivityType type = StandardActivityTypes.ToDo;
        registry.TryGet("ToDo", out Arg.Any<ActivityType?>()).Returns(call =>
        {
            call[1] = type;
            return true;
        });
        return registry;
    }

    [Fact]
    public void Create_initialises_open_activity_and_raises_assigned_events()
    {
        var activity = Activity.Create(
            id: Guid.NewGuid(),
            entityType: SampleEntityType,
            entityId: SampleEntityId,
            type: "ToDo",
            assignedToUserId: SampleAssignee,
            dueAt: Now.AddDays(1),
            registry: RegistryWithToDo(),
            createdByUserId: SampleCreator,
            description: "Call back the customer",
            tenantId: Guid.NewGuid());

        activity.Status.ShouldBe(ActivityStatus.Open);
        activity.AssignedToUserId.ShouldBe(SampleAssignee);
        activity.CompletedAt.ShouldBeNull();
        activity.CompletedByUserId.ShouldBeNull();

        activity.DomainEvents.OfType<ActivityAssignedEvent>().Count().ShouldBe(1);
        activity.IntegrationEvents.OfType<ActivityAssignedEto>().Count().ShouldBe(1);
    }

    [Fact]
    public void Create_throws_when_type_not_in_registry()
    {
        IActivityRegistry empty = Substitute.For<IActivityRegistry>();
        empty.TryGet(Arg.Any<string>(), out Arg.Any<ActivityType?>()).Returns(false);

        Should.Throw<ArgumentException>(() => Activity.Create(
            Guid.NewGuid(), SampleEntityType, SampleEntityId,
            type: "Quote", SampleAssignee, Now, empty));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_throws_when_entityType_blank(string entityType) =>
        Should.Throw<ArgumentException>(() => Activity.Create(
            Guid.NewGuid(), entityType, SampleEntityId, "ToDo",
            SampleAssignee, Now, RegistryWithToDo()));

    [Fact]
    public void Create_throws_when_entityId_empty() =>
        Should.Throw<ArgumentException>(() => Activity.Create(
            Guid.NewGuid(), SampleEntityType, Guid.Empty, "ToDo",
            SampleAssignee, Now, RegistryWithToDo()));

    [Fact]
    public void Create_throws_when_assignee_empty() =>
        Should.Throw<ArgumentException>(() => Activity.Create(
            Guid.NewGuid(), SampleEntityType, SampleEntityId, "ToDo",
            assignedToUserId: Guid.Empty, Now, RegistryWithToDo()));

    [Fact]
    public void Complete_transitions_to_done_and_raises_events()
    {
        var activity = Activity.Create(Guid.NewGuid(), SampleEntityType, SampleEntityId,
            "ToDo", SampleAssignee, Now, RegistryWithToDo());

        var completer = Guid.NewGuid();
        DateTimeOffset at = Now.AddHours(2);
        activity.Complete(completer, at);

        activity.Status.ShouldBe(ActivityStatus.Done);
        activity.CompletedAt.ShouldBe(at);
        activity.CompletedByUserId.ShouldBe(completer);
        activity.DomainEvents.OfType<ActivityCompletedEvent>().ShouldHaveSingleItem();
        activity.IntegrationEvents.OfType<ActivityCompletedEto>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Cancel_transitions_to_cancelled_and_raises_local_event_only()
    {
        var activity = Activity.Create(Guid.NewGuid(), SampleEntityType, SampleEntityId,
            "ToDo", SampleAssignee, Now, RegistryWithToDo());

        activity.Cancel(Guid.NewGuid(), Now);

        activity.Status.ShouldBe(ActivityStatus.Cancelled);
        activity.DomainEvents.OfType<ActivityCancelledEvent>().ShouldHaveSingleItem();
        // Cancel does NOT propagate to the integration bus — internal lifecycle only.
        activity.IntegrationEvents.OfType<ActivityCompletedEto>().ShouldBeEmpty();
    }

    [Fact]
    public void Reassign_changes_assignee_and_raises_event()
    {
        var activity = Activity.Create(Guid.NewGuid(), SampleEntityType, SampleEntityId,
            "ToDo", SampleAssignee, Now, RegistryWithToDo());

        var newAssignee = Guid.NewGuid();
        activity.Reassign(newAssignee);

        activity.AssignedToUserId.ShouldBe(newAssignee);
        ActivityReassignedEvent reassigned = activity.DomainEvents.OfType<ActivityReassignedEvent>().ShouldHaveSingleItem();
        reassigned.PreviousAssigneeUserId.ShouldBe(SampleAssignee);
        reassigned.NewAssigneeUserId.ShouldBe(newAssignee);
    }

    [Fact]
    public void Reassign_to_same_user_is_a_no_op()
    {
        var activity = Activity.Create(Guid.NewGuid(), SampleEntityType, SampleEntityId,
            "ToDo", SampleAssignee, Now, RegistryWithToDo());

        // Clear the create-time event to isolate the no-op assertion.
        int eventsBefore = activity.DomainEvents.Count;
        activity.Reassign(SampleAssignee);

        activity.DomainEvents.Count.ShouldBe(eventsBefore);
        activity.AssignedToUserId.ShouldBe(SampleAssignee);
    }

    [Fact]
    public void Reschedule_updates_due_date_and_raises_event()
    {
        DateTimeOffset originalDue = Now.AddDays(1);
        var activity = Activity.Create(Guid.NewGuid(), SampleEntityType, SampleEntityId,
            "ToDo", SampleAssignee, originalDue, RegistryWithToDo());

        DateTimeOffset newDue = Now.AddDays(7);
        activity.Reschedule(newDue);

        activity.DueAt.ShouldBe(newDue);
        ActivityRescheduledEvent ev = activity.DomainEvents.OfType<ActivityRescheduledEvent>().ShouldHaveSingleItem();
        ev.PreviousDueAt.ShouldBe(originalDue);
        ev.NewDueAt.ShouldBe(newDue);
    }

    [Theory]
    [InlineData("Complete")]
    [InlineData("Cancel")]
    [InlineData("Reassign")]
    [InlineData("Reschedule")]
    public void Behaviors_throw_when_activity_is_terminal(string operation)
    {
        var activity = Activity.Create(Guid.NewGuid(), SampleEntityType, SampleEntityId,
            "ToDo", SampleAssignee, Now, RegistryWithToDo());
        activity.Complete(Guid.NewGuid(), Now);

        Should.Throw<InvalidOperationException>(() =>
        {
            switch (operation)
            {
                case "Complete": activity.Complete(Guid.NewGuid(), Now); break;
                case "Cancel": activity.Cancel(Guid.NewGuid(), Now); break;
                case "Reassign": activity.Reassign(Guid.NewGuid()); break;
                case "Reschedule": activity.Reschedule(Now.AddDays(2)); break;
            }
        });
    }
}
