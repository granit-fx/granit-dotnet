using FluentValidation;
using Granit.Parties.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Parties.Endpoints.Validators;

internal sealed class PartySuspendRequestValidator : GranitValidator<PartySuspendRequest>
{
    public PartySuspendRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(512);
    }
}
