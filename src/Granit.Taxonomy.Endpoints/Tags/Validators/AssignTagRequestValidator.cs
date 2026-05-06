using FluentValidation;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Endpoints.Tags.Dtos;

namespace Granit.Taxonomy.Endpoints.Tags.Validators;

/// <summary>Validator for <see cref="AssignTagRequest"/>.</summary>
internal sealed class AssignTagRequestValidator : AbstractValidator<AssignTagRequest>
{
    public AssignTagRequestValidator()
    {
        RuleFor(x => x.TargetType)
            .NotEmpty()
            .MaximumLength(TagAssignment.MaxTargetTypeLength);

        RuleFor(x => x.TargetId)
            .NotEqual(Guid.Empty);
    }
}
