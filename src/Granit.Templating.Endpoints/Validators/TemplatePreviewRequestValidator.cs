using FluentValidation;
using Granit.Templating.Endpoints.Dtos;
using Granit.Templating.Endpoints.Internal;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Templating.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="TemplatePreviewRequest"/> body for the template preview endpoint.
/// </summary>
internal sealed class TemplatePreviewRequestValidator : GranitValidator<TemplatePreviewRequest>
{
    public TemplatePreviewRequestValidator()
    {
        RuleFor(x => x.Culture)
            .MaximumLength(TemplatingPatterns.MaxCultureLength)
            .Matches(TemplatingPatterns.Bcp47Pattern())
            .WithErrorCodeAndMessage("Granit:Validation:InvalidBcp47LanguageTag")
            .When(x => x.Culture is not null);
    }
}
