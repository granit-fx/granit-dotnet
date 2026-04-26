using Granit.DataProtection;
using Granit.Domain;

namespace Granit.Contacts.Domain;

/// <summary>
/// A phone number attached to a <see cref="Contact"/>. A contact may carry several phone
/// numbers (mobile + office + home + other). Exactly zero or one <see cref="IsPrimary"/>
/// entry is enforced by the aggregate.
/// </summary>
public sealed class ContactPhone : Entity
{
    private ContactPhone() { }

    /// <summary>Creates a new contact phone number.</summary>
    public static ContactPhone Create(
        Guid id,
        PhoneKind kind,
        string number,
        bool isPrimary = false,
        string? label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        return new ContactPhone
        {
            Id = id,
            Kind = kind,
            Number = number,
            IsPrimary = isPrimary,
            Label = label,
        };
    }

    /// <summary>The kind of phone (Mobile / Office / Home / Other).</summary>
    public PhoneKind Kind { get; private set; }

    /// <summary>The phone number (recommend E.164 format, e.g. <c>"+3221234567"</c>).</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string Number { get; private set; } = string.Empty;

    /// <summary>Whether this is the contact's primary phone.</summary>
    public bool IsPrimary { get; private set; }

    /// <summary>Optional user-supplied label.</summary>
    public string? Label { get; private set; }

    internal void MarkPrimary(bool isPrimary) => IsPrimary = isPrimary;

    internal void Replace(PhoneKind kind, string number, string? label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        Kind = kind;
        Number = number;
        Label = label;
    }
}
