using FluentValidation;
using Granit.Contacts.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Contacts.Endpoints.Validators;

internal sealed class ContactUpdateRequestValidator : GranitValidator<ContactUpdateRequest>
{
    public ContactUpdateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Website).MaximumLength(2048);
        RuleFor(x => x.Language).MaximumLength(16);
        RuleFor(x => x.Timezone).MaximumLength(64);
    }
}
