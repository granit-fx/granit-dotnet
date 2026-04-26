using FluentValidation;
using Granit.Contacts.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Contacts.Endpoints.Validators;

internal sealed class ContactCreateRequestValidator : GranitValidator<ContactCreateRequest>
{
    public ContactCreateRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.DefaultCurrency).NotEmpty().Length(3);
        RuleFor(x => x.Website).MaximumLength(2048);
        RuleFor(x => x.Language).MaximumLength(16);
        RuleFor(x => x.Timezone).MaximumLength(64);
        RuleFor(x => x.TaxId).MaximumLength(64);
        RuleFor(x => x.RegistrationNumber).MaximumLength(64);
    }
}
