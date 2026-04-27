using Granit.DataProtection;
using Granit.Domain;

namespace Granit.Parties.Domain;

/// <summary>
/// An email address attached to a <see cref="Party"/>. A contact may carry several
/// emails simultaneously (personal + billing + support + …). Exactly zero or one
/// <see cref="IsPrimary"/> entry is enforced by the aggregate.
/// </summary>
public sealed class PartyEmail : Entity
{
    private PartyEmail() { }

    /// <summary>Creates a new contact email.</summary>
    public static PartyEmail Create(
        Guid id,
        string address,
        bool isPrimary = false,
        string? label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        return new PartyEmail
        {
            Id = id,
            Address = address,
            IsPrimary = isPrimary,
            Label = label,
        };
    }

    /// <summary>The email address as the user supplied it — preserved verbatim for display,
    /// outbound mail, audit, and GDPR rectification. The dedup-friendly form lives in
    /// <see cref="CanonicalEmail"/>.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string Address { get; private set; } = string.Empty;

    /// <summary>The canonical (dedup-friendly) form of <see cref="Address"/>. Computed by
    /// the EF canonicalisation interceptor on save (Gmail dot/plus-tag stripping, lower-case,
    /// trim). Null when canonicalisation produces no usable key. Indexed for Tier-1
    /// deterministic duplicate detection (Epic #1280).</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string? CanonicalEmail { get; private set; }

    /// <summary>Whether this is the contact's primary email.</summary>
    public bool IsPrimary { get; private set; }

    /// <summary>Optional user-supplied label (<c>"personal"</c>, <c>"billing"</c>, <c>"support"</c>, …).</summary>
    public string? Label { get; private set; }

    internal void MarkPrimary(bool isPrimary) => IsPrimary = isPrimary;

    internal void Replace(string address, string? label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        Address = address;
        Label = label;
    }

    /// <summary>
    /// Sets the canonical form. Called exclusively by the EF canonicalisation interceptor
    /// at save time — never by aggregate logic, since the canonical form is a derived value
    /// recomputable from <see cref="Address"/>.
    /// </summary>
    internal void SetCanonicalEmail(string? canonicalEmail) => CanonicalEmail = canonicalEmail;
}
