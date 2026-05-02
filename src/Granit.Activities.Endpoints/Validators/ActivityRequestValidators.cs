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

public sealed class CompleteActivityRequestValidator : AbstractValidator<CompleteActivityRequest>
{
    public CompleteActivityRequestValidator()
    {
        RuleFor(x => x.CompletedAt).NotEqual(default(DateTimeOffset));
    }
}

public sealed class CancelActivityRequestValidator : AbstractValidator<CancelActivityRequest>
{
    public CancelActivityRequestValidator()
    {
        RuleFor(x => x.CancelledAt).NotEqual(default(DateTimeOffset));
    }
}

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
