using FluentValidation;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Endpoints.Categories.Dtos;

namespace Granit.Taxonomy.Endpoints.Categories.Validators;

internal sealed class AssignCategoryRequestValidator : AbstractValidator<AssignCategoryRequest>
{
    public AssignCategoryRequestValidator()
    {
        RuleFor(x => x.TargetType)
            .NotEmpty()
            .MaximumLength(CategoryAssignment.MaxTargetTypeLength);

        RuleFor(x => x.TargetId).NotEqual(Guid.Empty);
    }
}
