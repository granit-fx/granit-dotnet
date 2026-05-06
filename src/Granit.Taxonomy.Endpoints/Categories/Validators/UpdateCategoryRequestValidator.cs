using FluentValidation;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Endpoints.Categories.Dtos;

namespace Granit.Taxonomy.Endpoints.Categories.Validators;

internal sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Category.MaxNameLength)
            .Matches("^[^/]+$")
            .When(x => x.Name is not null);

        RuleFor(x => x.IconName)
            .MaximumLength(Category.MaxIconNameLength)
            .When(x => x.IconName is not null);
    }
}
