using FluentValidation;
using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Endpoints.Tags.Dtos;

namespace Granit.Taxonomy.Endpoints.Tags.Validators;

/// <summary>
/// Validator for <see cref="UpdateTagRequest"/>. Each property is independently optional;
/// supplying none is a no-op handled by the endpoint.
/// </summary>
internal sealed class UpdateTagRequestValidator : AbstractValidator<UpdateTagRequest>
{
    public UpdateTagRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Tag.MaxNameLength)
            .When(x => x.Name is not null);

        RuleFor(x => x.Color)
            .NotEmpty()
            .Matches("^#[0-9A-Fa-f]{6}$")
            .When(x => x.Color is not null);
    }
}
