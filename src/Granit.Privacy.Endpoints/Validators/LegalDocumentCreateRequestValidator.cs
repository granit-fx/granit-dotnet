using FluentValidation;
using Granit.Privacy.Endpoints.Dtos;

namespace Granit.Privacy.Endpoints.Validators;

/// <summary>
/// Validator for <see cref="LegalDocumentCreateRequest"/>.
/// </summary>
internal sealed class LegalDocumentCreateRequestValidator : AbstractValidator<LegalDocumentCreateRequest>
{
    public LegalDocumentCreateRequestValidator()
    {
        RuleFor(x => x.DocumentId)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.TemplateName)
            .MaximumLength(500);
    }
}
