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
    /// <summary>Maximum allowed template content length (1 MB).</summary>
    private const int MaxContentLength = 1_048_576;

    /// <summary>Supported MIME types for template content.</summary>
    private static readonly string[] SupportedMimeTypes = ["text/html", "text/plain"];

    public SaveTemplateRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty()
            .MaximumLength(MaxContentLength);

        RuleFor(x => x.MimeType)
            .NotEmpty()
            .MaximumLength(TemplatingPatterns.MaxMimeTypeLength)
            .Must(mime => SupportedMimeTypes.Contains(mime, StringComparer.OrdinalIgnoreCase))
            .WithErrorCodeAndMessage("Validation:UnsupportedMimeType");

        RuleFor(x => x.Name)
            .MaximumLength(TemplatingPatterns.MaxNameLength)
            .Matches(TemplatingPatterns.TemplateNamePattern())
            .WithErrorCodeAndMessage("Validation:InvalidTemplateNamePattern")
            .When(x => x.Name is not null);

        RuleFor(x => x.Culture)
            .MaximumLength(TemplatingPatterns.MaxCultureLength)
            .Matches(TemplatingPatterns.Bcp47Pattern())
            .WithErrorCodeAndMessage("Validation:InvalidBcp47LanguageTag")
            .When(x => x.Culture is not null);
    }
}
