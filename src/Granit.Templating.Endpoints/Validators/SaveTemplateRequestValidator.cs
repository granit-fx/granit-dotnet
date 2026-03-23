using FluentValidation;
using Granit.Templating.Endpoints.Dtos;
using Granit.Templating.Endpoints.Internal;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Templating.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="SaveTemplateRequest"/> body
/// for the POST/PUT template draft endpoints.
/// </summary>
internal sealed class SaveTemplateRequestValidator : GranitValidator<SaveTemplateRequest>
{
    public SaveTemplateRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty();

        RuleFor(x => x.MimeType)
            .NotEmpty()
            .MaximumLength(TemplatingPatterns.MaxMimeTypeLength);

        RuleFor(x => x.Name)
            .MaximumLength(TemplatingPatterns.MaxNameLength)
            .Matches(TemplatingPatterns.TemplateNamePattern())
            .WithErrorCodeAndMessage("Granit:Validation:InvalidTemplateNamePattern")
            .When(x => x.Name is not null);

        RuleFor(x => x.Culture)
            .MaximumLength(TemplatingPatterns.MaxCultureLength)
            .Matches(TemplatingPatterns.Bcp47Pattern())
            .WithErrorCodeAndMessage("Granit:Validation:InvalidBcp47LanguageTag")
            .When(x => x.Culture is not null);
    }
}
