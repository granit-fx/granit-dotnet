using Granit.Domain;

namespace Granit.Contacts.Domain;

/// <summary>
/// Maps a <see cref="Contact"/> to its identifier in an external system (Stripe customer ID,
/// Mollie customer ID, Odoo <c>res.partner</c> ID, Sage customer ID, …).
/// </summary>
/// <remarks>
/// One mapping per <c>(Contact, ProviderName)</c> tuple — enforced by a unique index in
/// <c>Granit.Contacts.EntityFrameworkCore</c> and defensively at the aggregate level.
/// See <see cref="ContactExternalProviderNames"/> for reserved provider names.
/// </remarks>
public sealed class ContactExternalMapping : Entity
{
    private ContactExternalMapping() { }

    /// <summary>Creates a new external mapping.</summary>
    public static ContactExternalMapping Create(Guid id, string providerName, string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        return new ContactExternalMapping
        {
            Id = id,
            ProviderName = providerName,
            ExternalId = externalId,
        };
    }

    /// <summary>Provider key — see <see cref="ContactExternalProviderNames"/>.</summary>
    public string ProviderName { get; private set; } = string.Empty;

    /// <summary>Provider-natural identifier (free-form string).</summary>
    public string ExternalId { get; private set; } = string.Empty;
}
