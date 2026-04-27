using FluentValidation;
using Granit.Parties.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Parties.Endpoints.Validators;

internal sealed class PartyEmailRequestValidator : GranitValidator<PartyEmailRequest>
{
    public PartyEmailRequestValidator()
    {
        RuleFor(x => x.Address).NotEmpty().MaximumLength(320).EmailAddress();
        RuleFor(x => x.Label).MaximumLength(64);
    }
}
