using System.Text.RegularExpressions;

namespace Granit.Core.Domain.ValueObjects;

/// <summary>
/// MIME content type (e.g. <c>text/html</c>, <c>application/pdf</c>).
/// Validates the <c>type/subtype</c> format per RFC 6838.
/// </summary>
public sealed partial class ContentType : SingleValueObject<string>
{
    /// <inheritdoc />
    public override required string Value { get; init; }

    /// <summary>
    /// Creates a validated <see cref="ContentType"/>.
    /// </summary>
    /// <exception cref="ArgumentException">When <paramref name="value"/> is not a valid MIME type.</exception>
    public static ContentType Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!MimeTypePattern().IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a valid MIME content type.", nameof(value));
        }

        return new ContentType { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="string"/>.</summary>
    public static implicit operator string(ContentType contentType) => contentType.Value;

    /// <summary>Implicit conversion from <see cref="string"/>.</summary>
    public static implicit operator ContentType(string value) => Create(value);

    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9!#$&\-^_.+]*\/[a-zA-Z0-9][a-zA-Z0-9!#$&\-^_.+]*$",
        RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MimeTypePattern();
}
