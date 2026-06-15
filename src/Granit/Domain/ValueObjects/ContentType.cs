using System.Text.RegularExpressions;

namespace Granit.Domain.ValueObjects;

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

    /// <summary>
    /// The top-level type — the part before the <c>/</c> (e.g. <c>image</c> for <c>image/png</c>).
    /// </summary>
    public string TopLevelType => Value[..Value.IndexOf('/')];

    /// <summary>
    /// The subtype — the part after the <c>/</c> (e.g. <c>png</c> for <c>image/png</c>).
    /// </summary>
    public string SubType => Value[(Value.IndexOf('/') + 1)..];

    /// <summary><c>true</c> when the top-level type is <c>image</c> (case-insensitive).</summary>
    public bool IsImage => IsTopLevelType("image");

    /// <summary><c>true</c> when the top-level type is <c>video</c> (case-insensitive).</summary>
    public bool IsVideo => IsTopLevelType("video");

    /// <summary><c>true</c> when the top-level type is <c>audio</c> (case-insensitive).</summary>
    public bool IsAudio => IsTopLevelType("audio");

    /// <summary><c>true</c> when the top-level type is <c>text</c> (case-insensitive).</summary>
    public bool IsText => IsTopLevelType("text");

    /// <summary>
    /// <c>true</c> when the top-level type equals <paramref name="topLevelType"/> (case-insensitive),
    /// e.g. <c>contentType.IsTopLevelType("application")</c>.
    /// </summary>
    /// <param name="topLevelType">The top-level type to compare against (without the trailing <c>/</c>).</param>
    public bool IsTopLevelType(string topLevelType) =>
        TopLevelType.Equals(topLevelType, StringComparison.OrdinalIgnoreCase);

    /// <summary>Implicit conversion to <see cref="string"/>.</summary>
    public static implicit operator string(ContentType contentType) => contentType.Value;

    /// <summary>Implicit conversion from <see cref="string"/>.</summary>
    public static implicit operator ContentType(string value) => Create(value);

    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9!#$&\-^_.+]*\/[a-zA-Z0-9][a-zA-Z0-9!#$&\-^_.+]*$",
        RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MimeTypePattern();
}
