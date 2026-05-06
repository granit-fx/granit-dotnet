using FluentValidation;
using Granit.Activities.Endpoints.Dtos;

namespace Granit.Activities.Endpoints.Validators;

/// <summary>
/// FluentValidation rules for <see cref="CreateActivityRequest"/>. Type
/// existence is checked at the aggregate boundary
/// (<c>Activity.Create</c> calls <c>IActivityRegistry.TryGet</c>) so we keep
/// the validator lightweight here.
/// </summary>
public sealed class CreateActivityRequestValidator : AbstractValidator<CreateActivityRequest>
{
    public CreateActivityRequestValidator()
    {
        RuleFor(x => x.EntityType).NotEmpty().MaximumLength(256);
        RuleFor(x => x.EntityId).NotEqual(Guid.Empty);
        RuleFor(x => x.Type).NotEmpty().MaximumLength(64);
        RuleFor(x => x.AssignedToUserId).NotEqual(Guid.Empty);
        RuleFor(x => x.Description).MaximumLength(2_000);
    }
}

// Empty validators — both request bodies carry no fields; the actor user id and
// timestamp are resolved server-side (VULN-101). The architecture test requires
// every *Request type to have a corresponding validator, so we ship no-op ones.
public sealed class CompleteActivityRequestValidator : AbstractValidator<CompleteActivityRequest>;

public sealed class CancelActivityRequestValidator : AbstractValidator<CancelActivityRequest>;

public sealed class ReassignActivityRequestValidator : AbstractValidator<ReassignActivityRequest>
{
    public ReassignActivityRequestValidator()
    {
        RuleFor(x => x.NewAssigneeUserId).NotEqual(Guid.Empty);
    }
}

public sealed class RescheduleActivityRequestValidator : AbstractValidator<RescheduleActivityRequest>
{
    public RescheduleActivityRequestValidator()
    {
        RuleFor(x => x.NewDueAt).NotEqual(default(DateTimeOffset));
    }
}

public sealed class ActivityCalendarRequestValidator : AbstractValidator<ActivityCalendarRequest>
{
    public ActivityCalendarRequestValidator()
    {
        RuleFor(x => x.From).NotEqual(default(DateTimeOffset));
        RuleFor(x => x.To).NotEqual(default(DateTimeOffset))
            .GreaterThan(x => x.From);
        RuleFor(x => x.EntityType).MaximumLength(256);
        RuleFor(x => x.Type).MaximumLength(512);
        RuleFor(x => x.Assignee).MaximumLength(64);
    }
}
