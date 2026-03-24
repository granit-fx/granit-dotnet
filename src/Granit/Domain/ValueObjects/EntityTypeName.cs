namespace Granit.Domain.ValueObjects;

/// <summary>
/// A CLR type name used to identify an entity type across modules
/// (e.g. <c>"Patient"</c>, <c>"Granit.BlobStorage.Domain.BlobDescriptor"</c>).
/// </summary>
public sealed class EntityTypeName : SingleValueObject<string>
{
    private const int MaxLength = 500;

    /// <inheritdoc />
    public override required string Value { get; init; }

    /// <summary>
    /// Creates a validated <see cref="EntityTypeName"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When <paramref name="value"/> is empty or exceeds 500 characters.
    /// </exception>
    public static EntityTypeName Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Entity type name exceeds maximum length of {MaxLength} characters.", nameof(value));
        }

        return new EntityTypeName { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="string"/>.</summary>
    public static implicit operator string(EntityTypeName name) => name.Value;

    /// <summary>Implicit conversion from <see cref="string"/>.</summary>
    public static implicit operator EntityTypeName(string value) => Create(value);
}
