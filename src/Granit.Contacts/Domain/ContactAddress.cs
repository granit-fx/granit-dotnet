using Granit.Domain;

namespace Granit.Contacts.Domain;

/// <summary>
/// A typed address attached to a <see cref="Contact"/>. A contact may carry several
/// addresses simultaneously (one for billing, one for shipping, others for HQ /
/// returns / branches). Exactly zero or one <see cref="IsDefault"/> entry per
/// <see cref="AddressKind"/> is enforced by the aggregate.
/// </summary>
public sealed class ContactAddress : Entity
{
    private ContactAddress() { }

    /// <summary>Creates a new contact address.</summary>
    public static ContactAddress Create(
        Guid id,
        AddressKind kind,
        Address value,
        bool isDefault = false,
        string? label = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new ContactAddress
        {
            Id = id,
            Kind = kind,
            Value = value,
            IsDefault = isDefault,
            Label = label,
        };
    }

    /// <summary>The functional purpose of the address.</summary>
    public AddressKind Kind { get; private set; }

    /// <summary>The actual postal address (owned value object).</summary>
    public Address Value { get; private set; } = null!;

    /// <summary>Whether this is the default address for its <see cref="Kind"/>.</summary>
    public bool IsDefault { get; private set; }

    /// <summary>Optional user-supplied label (e.g., <c>"HQ"</c>, <c>"Warehouse #2"</c>).</summary>
    public string? Label { get; private set; }

    internal void MarkDefault(bool isDefault) => IsDefault = isDefault;

    internal void Replace(Address value, string? label)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
        Label = label;
    }
}
