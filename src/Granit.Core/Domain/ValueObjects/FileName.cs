namespace Granit.Core.Domain.ValueObjects;

/// <summary>
/// A file name validated for safety: non-empty, max 260 characters, no path traversal.
/// </summary>
public sealed class FileName : SingleValueObject<string>
{
    private const int MaxLength = 260;

    /// <inheritdoc />
    public override required string Value { get; init; }

    /// <summary>
    /// Creates a validated <see cref="FileName"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When <paramref name="value"/> is empty, exceeds 260 characters, or contains path traversal sequences.
    /// </exception>
    public static FileName Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MaxLength)
        {
            throw new ArgumentException(
                $"File name exceeds maximum length of {MaxLength} characters.", nameof(value));
        }

        if (value.Contains("..", StringComparison.Ordinal)
            || value.Contains('/')
            || value.Contains('\\'))
        {
            throw new ArgumentException(
                "File name must not contain path traversal sequences.", nameof(value));
        }

        return new FileName { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="string"/>.</summary>
    public static implicit operator string(FileName fileName) => fileName.Value;

    /// <summary>Implicit conversion from <see cref="string"/>.</summary>
    public static implicit operator FileName(string value) => Create(value);
}
