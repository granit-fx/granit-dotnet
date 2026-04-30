using FluentValidation;
using Granit.Entities.Views.Endpoints.Dtos;
using Granit.Validation;

namespace Granit.Entities.Views.Endpoints.Validators;

internal sealed class EntityViewUpdateBodyRequestValidator : GranitValidator<EntityViewUpdateBodyRequest>
{
    public EntityViewUpdateBodyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Icon).MaximumLength(64);
        RuleFor(x => x.State).NotNull();
    }
}
