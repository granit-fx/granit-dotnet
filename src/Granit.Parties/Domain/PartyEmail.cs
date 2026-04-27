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

    /// <summary>The email address.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string Address { get; private set; } = string.Empty;

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
}
