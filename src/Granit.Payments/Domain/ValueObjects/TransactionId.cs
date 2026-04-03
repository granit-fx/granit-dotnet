using Granit.Domain;

namespace Granit.Payments.Domain.ValueObjects;

/// <summary>Strongly-typed identifier for a <see cref="PaymentTransaction"/>.</summary>
public sealed class TransactionId : SingleValueObject<Guid>
{
    /// <inheritdoc />
    public override required Guid Value { get; init; }

    public static TransactionId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Transaction identifier must not be empty.", nameof(value));
        }

        return new TransactionId { Value = value };
    }

    public static implicit operator Guid(TransactionId id) => id.Value;

    public static implicit operator TransactionId(Guid value) => Create(value);
}
