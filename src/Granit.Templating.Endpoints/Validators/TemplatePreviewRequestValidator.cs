using System.Text.RegularExpressions;
using FluentValidation;
using Granit.Templating.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Templating.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="TemplatePreviewRequest"/> body for the template preview endpoint.
/// </summary>
internal sealed partial class TemplatePreviewRequestValidator : GranitValidator<TemplatePreviewRequest>
{
    /// <summary>Maximum length for culture tag (must match <c>SaveTemplateRequestValidator</c>).</summary>
    internal const int MaxCultureLength = 10;

    // BCP 47 language tag.
    [GeneratedRegex(@"^[a-zA-Z]{2,8}(-[a-zA-Z0-9]{1,8})*$")]
    private static partial Regex Bcp47Pattern();

    public TemplatePreviewRequestValidator()
    {
        RuleFor(x => x.Culture)
            .MaximumLength(MaxCultureLength)
            .Matches(Bcp47Pattern())
            .WithErrorCodeAndMessage("Granit:Validation:InvalidBcp47LanguageTag")
            .When(x => x.Culture is not null);
    }
}
