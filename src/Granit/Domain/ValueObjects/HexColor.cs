using System.Text.RegularExpressions;

namespace Granit.Domain.ValueObjects;

/// <summary>
/// A colour expressed as a hex code with a leading <c>#</c> and 3, 4, 6 or 8 hex digits
/// (<c>#RGB</c>, <c>#RGBA</c>, <c>#RRGGBB</c>, <c>#RRGGBBAA</c>). Trimmed and normalised to
/// upper-case. The accepted set matches the framework <c>Validation:Format:ColorHex</c> server
/// validator, so a value that passes endpoint validation is always constructible here. EF Core and
/// JSON conversions are applied automatically (<c>ApplyGranitConventions</c> /
/// <see cref="Json.SingleValueObjectJsonConverterFactory"/>).
/// </summary>
public sealed partial class HexColor : SingleValueObject<string>
{
    /// <inheritdoc />
    public override required string Value { get; init; }

    /// <summary>
    /// Creates a validated, upper-cased <see cref="HexColor"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When <paramref name="value"/> is not a <c>#</c>-prefixed 3/4/6/8-digit hex colour.
    /// </exception>
    public static HexColor Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string trimmed = value.Trim();
        if (!HexPattern().IsMatch(trimmed))
        {
            throw new ArgumentException(
                "Hex colour must be '#' followed by 3, 4, 6 or 8 hex digits (e.g. '#8B5CF6').", nameof(value));
        }

        return new HexColor { Value = trimmed.ToUpperInvariant() };
    }

    /// <summary>Implicit conversion to <see cref="string"/>.</summary>
    public static implicit operator string(HexColor color) => color.Value;

    /// <summary>Implicit conversion from <see cref="string"/>.</summary>
    public static implicit operator HexColor(string value) => Create(value);

    // Mirrors FormatValidatorExtensions.ColorHexRegex in Granit.Validation (3/4/6/8 hex digits).
    [GeneratedRegex("^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{4}|[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$")]
    private static partial Regex HexPattern();
}
