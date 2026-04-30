using FluentValidation;
using Granit.Entities.Views.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Entities.Views.Endpoints.Validators;

internal sealed class EntityViewShareBodyRequestValidator : GranitValidator<EntityViewShareBodyRequest>
{
    public EntityViewShareBodyRequestValidator()
    {
        RuleFor(x => x.Roles).NotNull();
        RuleFor(x => x.Users).NotNull();
        RuleFor(x => x).Must(r => r.Roles.Count > 0 || r.Users.Count > 0)
            .WithErrorCode("Granit:Validation:EntityViewShareEmptyAudience");
    }
}
