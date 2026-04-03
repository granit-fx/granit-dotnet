using Granit.Domain;

namespace Granit.Payments.Domain.ValueObjects;

/// <summary>Strongly-typed identifier for a <see cref="PaymentMethod"/>.</summary>
public sealed class PaymentMethodId : SingleValueObject<Guid>
{
    /// <inheritdoc />
    public override required Guid Value { get; init; }

    public static PaymentMethodId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Payment method identifier must not be empty.", nameof(value));
        }

        return new PaymentMethodId { Value = value };
    }

    public static implicit operator Guid(PaymentMethodId id) => id.Value;

    public static implicit operator PaymentMethodId(Guid value) => Create(value);
}
