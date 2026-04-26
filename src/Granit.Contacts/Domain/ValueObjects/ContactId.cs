using Granit.Domain;

namespace Granit.Contacts.Domain.ValueObjects;

/// <summary>Strongly-typed identifier for a <see cref="Contact"/>.</summary>
public sealed class ContactId : SingleValueObject<Guid>
{
    /// <inheritdoc />
    public override required Guid Value { get; init; }

    /// <summary>Creates a new <see cref="ContactId"/>.</summary>
    public static ContactId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Contact identifier must not be empty.", nameof(value));
        }

        return new ContactId { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="Guid"/>.</summary>
    public static implicit operator Guid(ContactId id) => id.Value;

    /// <summary>Implicit conversion from <see cref="Guid"/>.</summary>
    public static implicit operator ContactId(Guid value) => Create(value);
}
