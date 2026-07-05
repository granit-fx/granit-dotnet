using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Granit.Validation.Formats;

/// <summary>
/// Canonical URL-friendly slug format, shared by request validation (the <c>.Slug()</c>
/// FluentValidation rule) and by domain-layer invariants that cannot take a FluentValidation
/// dependency. A slug is lower-case ASCII letters, digits and single hyphens, with no leading,
/// trailing or consecutive hyphens. Examples: <c>my-blog-post</c>, <c>tenant-42</c>, <c>product</c>.
/// </summary>
public static partial class SlugFormat
{
    // 100 ms ReDoS timeout — the pattern is linear, the bound is purely defensive.
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.None, 100)]
    private static partial Regex SlugRegex();

    /// <summary>Returns <c>true</c> when <paramref name="value"/> is a valid slug.</summary>
    public static bool IsValid([NotNullWhen(true)] string? value) =>
        value is not null && SlugRegex().IsMatch(value);
}
