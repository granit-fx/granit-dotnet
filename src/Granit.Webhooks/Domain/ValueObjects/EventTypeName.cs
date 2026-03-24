using System.Text.RegularExpressions;
using Granit.Domain;

namespace Granit.Webhooks.Domain.ValueObjects;

/// <summary>
/// A dotted event type identifier (e.g. <c>document.uploaded</c>, <c>patient.created</c>).
/// Maximum 200 characters.
/// </summary>
public sealed partial class EventTypeName : SingleValueObject<string>
{
    private const int MaxLength = 200;

    /// <inheritdoc />
    public override required string Value { get; init; }

    /// <summary>
    /// Creates a validated <see cref="EventTypeName"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When <paramref name="value"/> is empty, exceeds 200 characters, or is not a valid dotted identifier.
    /// </exception>
    public static EventTypeName Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Event type name exceeds maximum length of {MaxLength} characters.", nameof(value));
        }

        if (!DottedIdentifierPattern().IsMatch(value))
        {
            throw new ArgumentException(
                $"'{value}' is not a valid dotted event type name (expected format: 'a.b.c').", nameof(value));
        }

        return new EventTypeName { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="string"/>.</summary>
    public static implicit operator string(EventTypeName name) => name.Value;

    /// <summary>Implicit conversion from <see cref="string"/>.</summary>
    public static implicit operator EventTypeName(string value) => Create(value);

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9_-]*(\.[a-zA-Z][a-zA-Z0-9_-]*)+$",
        RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DottedIdentifierPattern();
}
