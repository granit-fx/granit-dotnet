using FluentValidation;
using Granit.Subscriptions.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Subscriptions.Endpoints.Validators;

/// <summary>
/// Validates <see cref="PlanCreateRequest"/>.
/// </summary>
internal sealed class PlanCreateRequestValidator : GranitValidator<PlanCreateRequest>
{
    internal const int MaxNameLength = 200;
    internal const int MaxDescriptionLength = 2000;

    public PlanCreateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(MaxNameLength);

        RuleFor(x => x.Description)
            .MaximumLength(MaxDescriptionLength)
            .When(x => x.Description is not null);

        RuleFor(x => x.PricingModel)
            .NotEmpty();

        RuleFor(x => x.DefaultInterval)
            .NotEmpty();

        RuleFor(x => x.TrialDays)
            .GreaterThanOrEqualTo(0)
            .When(x => x.TrialDays.HasValue);

        RuleFor(x => x.SeatLimit)
            .GreaterThan(0)
            .When(x => x.SeatLimit.HasValue);
    }
}
