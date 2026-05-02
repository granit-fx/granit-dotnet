namespace Granit.Domain.ValueObjects;

/// <summary>
/// An opaque identifier for a blob produced by a <c>Granit.BlobStorage</c>
/// provider (S3 key, Azure Blob name, file-system path, …). Carried across
/// module boundaries — domain aggregates, distributed events, HTTP DTOs — as
/// a typed value rather than a bare <see cref="string"/> so the contract is
/// self-documenting and primitive-obsession is avoided.
/// </summary>
/// <remarks>
/// Persisted as the underlying <see cref="string"/> via the framework's
/// <c>SingleValueObjectConverter</c> (column type unchanged: <c>nvarchar(500)</c>
/// or equivalent). JSON-serialized as a plain string via
/// <c>SingleValueObjectJsonConverterFactory</c>, so wire format with Wolverine
/// outboxes and HTTP clients stays identical to the previous <c>string</c>-typed
/// shape.
/// </remarks>
public sealed class BlobReference : SingleValueObject<string>
{
    /// <summary>Maximum length tolerated for a blob reference identifier.</summary>
    public const int MaxLength = 500;

    /// <inheritdoc />
    public override required string Value { get; init; }

    /// <summary>
    /// Creates a validated <see cref="BlobReference"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When <paramref name="value"/> is empty, whitespace-only, or exceeds
    /// <see cref="MaxLength"/> characters.
    /// </exception>
    public static BlobReference Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Blob reference exceeds maximum length of {MaxLength} characters.", nameof(value));
        }

        return new BlobReference { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="string"/>.</summary>
    public static implicit operator string(BlobReference blobReference) => blobReference.Value;

    /// <summary>Implicit conversion from <see cref="string"/>.</summary>
    public static implicit operator BlobReference(string value) => Create(value);
}
