using FluentValidation;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Endpoints.Categories.Dtos;

namespace Granit.Taxonomy.Endpoints.Categories.Validators;

internal sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Scope)
            .NotEmpty()
            .MaximumLength(Category.MaxScopeLength);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Category.MaxNameLength)
            .Matches("^[^/]+$");

        RuleFor(x => x.IconName)
            .MaximumLength(Category.MaxIconNameLength)
            .When(x => x.IconName is not null);
    }
}
