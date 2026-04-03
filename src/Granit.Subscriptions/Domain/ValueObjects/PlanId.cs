using Granit.Domain;

namespace Granit.Subscriptions.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a <see cref="Plan"/>.
/// </summary>
public sealed class PlanId : SingleValueObject<Guid>
{
    /// <inheritdoc />
    public override required Guid Value { get; init; }

    /// <summary>Creates a new <see cref="PlanId"/>.</summary>
    public static PlanId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Plan identifier must not be empty.", nameof(value));
        }

        return new PlanId { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="Guid"/>.</summary>
    public static implicit operator Guid(PlanId id) => id.Value;

    /// <summary>Implicit conversion from <see cref="Guid"/>.</summary>
    public static implicit operator PlanId(Guid value) => Create(value);
}
