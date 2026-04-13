using FluentValidation;
using Granit.Subscriptions.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Subscriptions.Endpoints.Validators;

/// <summary>
/// Validates <see cref="PlanUpdateRequest"/>.
/// </summary>
internal sealed class PlanUpdateRequestValidator : GranitValidator<PlanUpdateRequest>
{
    internal const int MaxNameLength = 200;
    internal const int MaxDescriptionLength = 2000;

    public PlanUpdateRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(MaxNameLength);

        RuleFor(x => x.Description)
            .MaximumLength(MaxDescriptionLength)
            .When(x => x.Description is not null);

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0);
    }
}
