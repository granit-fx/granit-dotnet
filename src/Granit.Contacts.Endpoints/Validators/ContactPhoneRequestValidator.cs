using FluentValidation;
using Granit.Contacts.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Contacts.Endpoints.Validators;

internal sealed class ContactPhoneRequestValidator : GranitValidator<ContactPhoneRequest>
{
    public ContactPhoneRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Number).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Label).MaximumLength(64);
    }
}
