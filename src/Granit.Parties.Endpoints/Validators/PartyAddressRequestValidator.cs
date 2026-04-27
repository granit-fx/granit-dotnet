using FluentValidation;
using Granit.Parties.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Parties.Endpoints.Validators;

internal sealed class PartyAddressRequestValidator : GranitValidator<PartyAddressRequest>
{
    public PartyAddressRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Line2).MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Country).NotEmpty().Length(2);
        RuleFor(x => x.CompanyName).MaximumLength(200);
        RuleFor(x => x.State).MaximumLength(100);
        RuleFor(x => x.Label).MaximumLength(128);
    }
}
