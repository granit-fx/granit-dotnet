using FluentValidation;
using Granit.Taxonomy.Endpoints.Categories.Dtos;

namespace Granit.Taxonomy.Endpoints.Categories.Validators;

internal sealed class MoveCategoryRequestValidator : AbstractValidator<MoveCategoryRequest>
{
    public MoveCategoryRequestValidator()
    {
        RuleFor(x => x.NewParentId)
            .NotEqual(Guid.Empty)
            .When(x => x.NewParentId.HasValue);
    }
}
