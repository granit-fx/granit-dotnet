using FluentValidation;
using Granit.Privacy.Endpoints.Dtos;

namespace Granit.Privacy.Endpoints.Validators;

/// <summary>
/// Validator for <see cref="LegalDocumentUpdateRequest"/>.
/// </summary>
internal sealed class LegalDocumentUpdateRequestValidator : AbstractValidator<LegalDocumentUpdateRequest>
{
    public LegalDocumentUpdateRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(500);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.TemplateName)
            .MaximumLength(500);
    }
}
