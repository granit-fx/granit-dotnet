using FluentValidation;
using Granit.Parties.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Parties.Endpoints.Validators;

internal sealed class PartyPhoneRequestValidator : GranitValidator<PartyPhoneRequest>
{
    public PartyPhoneRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Number).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Label).MaximumLength(64);
    }
}
