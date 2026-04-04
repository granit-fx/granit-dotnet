using Granit.Domain;

namespace Granit.CustomerBalance.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a <see cref="BalanceAccount"/>.
/// </summary>
public sealed class BalanceAccountId : SingleValueObject<Guid>
{
    /// <inheritdoc />
    public override required Guid Value { get; init; }

    /// <summary>Creates a new <see cref="BalanceAccountId"/>.</summary>
    public static BalanceAccountId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Balance account identifier must not be empty.", nameof(value));
        }

        return new BalanceAccountId { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="Guid"/>.</summary>
    public static implicit operator Guid(BalanceAccountId id) => id.Value;

    /// <summary>Implicit conversion from <see cref="Guid"/>.</summary>
    public static implicit operator BalanceAccountId(Guid value) => Create(value);
}
