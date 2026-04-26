using FluentValidation;
using Granit.Contacts.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Contacts.Endpoints.Validators;

internal sealed class ContactEmailRequestValidator : GranitValidator<ContactEmailRequest>
{
    public ContactEmailRequestValidator()
    {
        RuleFor(x => x.Address).NotEmpty().MaximumLength(320).EmailAddress();
        RuleFor(x => x.Label).MaximumLength(64);
    }
}
