using FluentValidation;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Endpoints.Tags.Dtos;

namespace Granit.Taxonomy.Endpoints.Tags.Validators;

/// <summary>Validator for <see cref="CreateTagRequest"/>.</summary>
internal sealed class CreateTagRequestValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagRequestValidator()
    {
        RuleFor(x => x.Scope)
            .NotEmpty()
            .MaximumLength(Tag.MaxScopeLength);

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Tag.MaxNameLength);

        RuleFor(x => x.Color)
            .NotEmpty()
            .Matches("^#[0-9A-Fa-f]{6}$");
    }
}
