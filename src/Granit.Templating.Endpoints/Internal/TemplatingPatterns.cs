using System.Text.RegularExpressions;

namespace Granit.Templating.Endpoints.Internal;

/// <summary>
/// Shared compiled regex patterns for template name and culture validation.
/// Used by both FluentValidation validators and endpoint route parameter helpers.
/// </summary>
internal static partial class TemplatingPatterns
{
    /// <summary>Maximum length for template name (must match <c>TemplateRevisionEntityConfiguration</c>).</summary>
    internal const int MaxNameLength = 200;

    /// <summary>Maximum length for culture tag (must match <c>TemplateRevisionEntityConfiguration</c>).</summary>
    internal const int MaxCultureLength = 10;

    /// <summary>Maximum length for MIME type (must match <c>TemplateRevisionEntityConfiguration</c>).</summary>
    internal const int MaxMimeTypeLength = 127;

    /// <summary>Template name: "Domain.Name" pattern — alphanumeric with dots.</summary>
    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z][A-Za-z0-9]*)+$")]
    internal static partial Regex TemplateNamePattern();

    /// <summary>BCP 47 language tag.</summary>
    [GeneratedRegex(@"^[a-zA-Z]{2,8}(-[a-zA-Z0-9]{1,8})*$")]
    internal static partial Regex Bcp47Pattern();
}
