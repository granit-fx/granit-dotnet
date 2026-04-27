using Granit.DataProtection;
using Granit.Domain;

namespace Granit.Parties.Domain;

/// <summary>
/// A phone number attached to a <see cref="Party"/>. A contact may carry several phone
/// numbers (mobile + office + home + other). Exactly zero or one <see cref="IsPrimary"/>
/// entry is enforced by the aggregate.
/// </summary>
public sealed class PartyPhone : Entity
{
    private PartyPhone() { }

    /// <summary>Creates a new contact phone number.</summary>
    public static PartyPhone Create(
        Guid id,
        PhoneKind kind,
        string number,
        bool isPrimary = false,
        string? label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        return new PartyPhone
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

    /// <summary>The phone number as the user supplied it — preserved verbatim for display,
    /// outbound calls / SMS, audit. The dedup-friendly form lives in
    /// <see cref="CanonicalNumber"/>.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string Number { get; private set; } = string.Empty;

    /// <summary>The canonical (dedup-friendly) E.164 form of <see cref="Number"/>. Computed by
    /// the EF canonicalisation interceptor on save via libphonenumber. Null when parsing fails
    /// or the input is missing a country code prefix. Indexed for Tier-1 deterministic
    /// duplicate detection (Epic #1280).</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string? CanonicalNumber { get; private set; }

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

    /// <summary>
    /// Sets the canonical E.164 form. Called exclusively by the EF canonicalisation interceptor
    /// at save time — never by aggregate logic, since the canonical form is a derived value
    /// recomputable from <see cref="Number"/>.
    /// </summary>
    internal void SetCanonicalNumber(string? canonicalNumber) => CanonicalNumber = canonicalNumber;
}
