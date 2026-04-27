using FluentValidation;
using Granit.Parties.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Parties.Endpoints.Validators;

internal sealed class PartyTaxStatusRequestValidator : GranitValidator<PartyTaxStatusRequest>
{
    public PartyTaxStatusRequestValidator()
    {
        RuleFor(x => x.Vatin).MaximumLength(32);

        // Reverse-charge requires a buyer-side VAT identification number.
        RuleFor(x => x.Vatin)
            .NotEmpty()
            .When(x => x.ReverseCharge);
    }
}
