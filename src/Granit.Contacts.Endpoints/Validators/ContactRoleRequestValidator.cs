using FluentValidation;
using Granit.Contacts.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Contacts.Endpoints.Validators;

internal sealed class ContactRoleRequestValidator : GranitValidator<ContactRoleRequest>
{
    public ContactRoleRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}
