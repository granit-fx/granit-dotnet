using FluentValidation;
using Granit.Subscriptions.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Subscriptions.Endpoints.Validators;

/// <summary>
/// Validates <see cref="SeatAssignRequest"/>.
/// </summary>
internal sealed class SeatAssignRequestValidator : GranitValidator<SeatAssignRequest>
{
    public SeatAssignRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();
    }
}
