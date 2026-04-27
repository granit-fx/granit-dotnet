using FluentValidation;
using Granit.Contacts.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Contacts.Endpoints.Validators;

internal sealed class ContactSuspendRequestValidator : GranitValidator<ContactSuspendRequest>
{
    public ContactSuspendRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(512);
    }
}
