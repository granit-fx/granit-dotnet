using Granit.Domain;

namespace Granit.Invoicing.Domain.ValueObjects;

/// <summary>Strongly-typed identifier for an <see cref="Invoice"/>.</summary>
public sealed class InvoiceId : SingleValueObject<Guid>
{
    /// <inheritdoc />
    public override required Guid Value { get; init; }

    /// <summary>Creates a new <see cref="InvoiceId"/>.</summary>
    public static InvoiceId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Invoice identifier must not be empty.", nameof(value));
        }

        return new InvoiceId { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="Guid"/>.</summary>
    public static implicit operator Guid(InvoiceId id) => id.Value;

    /// <summary>Implicit conversion from <see cref="Guid"/>.</summary>
    public static implicit operator InvoiceId(Guid value) => Create(value);
}
