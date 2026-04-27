using Granit.Domain;

namespace Granit.Parties.Domain.ValueObjects;

/// <summary>Strongly-typed identifier for a <see cref="Party"/>.</summary>
public sealed class PartyId : SingleValueObject<Guid>
{
    /// <inheritdoc />
    public override required Guid Value { get; init; }

    /// <summary>Creates a new <see cref="PartyId"/>.</summary>
    public static PartyId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Party identifier must not be empty.", nameof(value));
        }

        return new PartyId { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="Guid"/>.</summary>
    public static implicit operator Guid(PartyId id) => id.Value;

    /// <summary>Implicit conversion from <see cref="Guid"/>.</summary>
    public static implicit operator PartyId(Guid value) => Create(value);
}
