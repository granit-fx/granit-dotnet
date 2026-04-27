using Granit.Domain;

namespace Granit.Parties.Domain;

/// <summary>
/// Maps a <see cref="Party"/> to its identifier in an external system (Stripe customer ID,
/// Mollie customer ID, Odoo <c>res.partner</c> ID, Sage customer ID, …).
/// </summary>
/// <remarks>
/// One mapping per <c>(Party, ProviderName)</c> tuple — enforced by a unique index in
/// <c>Granit.Parties.EntityFrameworkCore</c> and defensively at the aggregate level.
/// See <see cref="PartyExternalProviderNames"/> for reserved provider names.
/// </remarks>
public sealed class PartyExternalMapping : Entity
{
    private PartyExternalMapping() { }

    /// <summary>Creates a new external mapping.</summary>
    public static PartyExternalMapping Create(Guid id, string providerName, string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        return new PartyExternalMapping
        {
            Id = id,
            ProviderName = providerName,
            ExternalId = externalId,
        };
    }

    /// <summary>Provider key — see <see cref="PartyExternalProviderNames"/>.</summary>
    public string ProviderName { get; private set; } = string.Empty;

    /// <summary>Provider-natural identifier (free-form string).</summary>
    public string ExternalId { get; private set; } = string.Empty;
}
