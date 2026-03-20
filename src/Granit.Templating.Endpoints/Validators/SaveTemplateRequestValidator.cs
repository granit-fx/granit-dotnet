using System.Text.RegularExpressions;
using FluentValidation;
using Granit.Templating.Endpoints.Dtos;
using Granit.Validation;
using Granit.Validation.Extensions;

namespace Granit.Templating.Endpoints.Validators;

/// <summary>
/// Validates the <see cref="SaveTemplateRequest"/> body
/// for the POST/PUT template draft endpoints.
/// </summary>
internal sealed partial class SaveTemplateRequestValidator : GranitValidator<SaveTemplateRequest>
{
    /// <summary>Maximum length for template name (must match <c>TemplateRevisionEntityConfiguration</c>).</summary>
    internal const int MaxNameLength = 200;

    /// <summary>Maximum length for culture tag (must match <c>TemplateRevisionEntityConfiguration</c>).</summary>
    internal const int MaxCultureLength = 10;

    /// <summary>Maximum length for MIME type (must match <c>TemplateRevisionEntityConfiguration</c>).</summary>
    internal const int MaxMimeTypeLength = 127;

    // Template name: "Domain.Name" pattern — alphanumeric with dots.
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)+$")]
    private static partial Regex TemplateNamePattern();

    // BCP 47 language tag.
    [GeneratedRegex(@"^[a-zA-Z]{2,8}(-[a-zA-Z0-9]{1,8})*$")]
    private static partial Regex Bcp47Pattern();

    public SaveTemplateRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty();

        RuleFor(x => x.MimeType)
            .NotEmpty()
            .MaximumLength(MaxMimeTypeLength);

        RuleFor(x => x.Name)
            .MaximumLength(MaxNameLength)
            .Matches(TemplateNamePattern())
            .WithErrorCodeAndMessage("Granit:Validation:InvalidTemplateNamePattern")
            .When(x => x.Name is not null);

        RuleFor(x => x.Culture)
            .MaximumLength(MaxCultureLength)
            .Matches(Bcp47Pattern())
            .WithErrorCodeAndMessage("Granit:Validation:InvalidBcp47LanguageTag")
            .When(x => x.Culture is not null);
    }
}
