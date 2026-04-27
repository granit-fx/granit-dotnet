using FluentValidation;
using Granit.Parties.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Parties.Endpoints.Validators;

internal sealed class PartyRoleRequestValidator : GranitValidator<PartyRoleRequest>
{
    public PartyRoleRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}
