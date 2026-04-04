using Granit.Domain;

namespace Granit.CustomerBalance.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a <see cref="BalanceTransaction"/>.
/// </summary>
public sealed class BalanceTransactionId : SingleValueObject<Guid>
{
    /// <inheritdoc />
    public override required Guid Value { get; init; }

    /// <summary>Creates a new <see cref="BalanceTransactionId"/>.</summary>
    public static BalanceTransactionId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Balance transaction identifier must not be empty.", nameof(value));
        }

        return new BalanceTransactionId { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="Guid"/>.</summary>
    public static implicit operator Guid(BalanceTransactionId id) => id.Value;

    /// <summary>Implicit conversion from <see cref="Guid"/>.</summary>
    public static implicit operator BalanceTransactionId(Guid value) => Create(value);
}
