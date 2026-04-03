using Granit.Domain;

namespace Granit.Subscriptions.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a <see cref="Subscription"/>.
/// </summary>
public sealed class SubscriptionId : SingleValueObject<Guid>
{
    /// <inheritdoc />
    public override required Guid Value { get; init; }

    /// <summary>Creates a new <see cref="SubscriptionId"/>.</summary>
    public static SubscriptionId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Subscription identifier must not be empty.", nameof(value));
        }

        return new SubscriptionId { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="Guid"/>.</summary>
    public static implicit operator Guid(SubscriptionId id) => id.Value;

    /// <summary>Implicit conversion from <see cref="Guid"/>.</summary>
    public static implicit operator SubscriptionId(Guid value) => Create(value);
}
