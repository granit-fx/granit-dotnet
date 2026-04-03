using Granit.Domain;

namespace Granit.Metering.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a <see cref="MeterDefinition"/>.
/// </summary>
public sealed class MeterDefinitionId : SingleValueObject<Guid>
{
    /// <inheritdoc />
    public override required Guid Value { get; init; }

    /// <summary>Creates a new <see cref="MeterDefinitionId"/>.</summary>
    public static MeterDefinitionId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Meter definition identifier must not be empty.", nameof(value));
        }

        return new MeterDefinitionId { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="Guid"/>.</summary>
    public static implicit operator Guid(MeterDefinitionId id) => id.Value;

    /// <summary>Implicit conversion from <see cref="Guid"/>.</summary>
    public static implicit operator MeterDefinitionId(Guid value) => Create(value);
}
