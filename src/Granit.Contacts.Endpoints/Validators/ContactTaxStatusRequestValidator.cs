using FluentValidation;
using Granit.Contacts.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Contacts.Endpoints.Validators;

internal sealed class ContactTaxStatusRequestValidator : GranitValidator<ContactTaxStatusRequest>
{
    public ContactTaxStatusRequestValidator()
    {
        RuleFor(x => x.Vatin).MaximumLength(32);

        // Reverse-charge requires a buyer-side VAT identification number.
        RuleFor(x => x.Vatin)
            .NotEmpty()
            .When(x => x.ReverseCharge);
    }
}
