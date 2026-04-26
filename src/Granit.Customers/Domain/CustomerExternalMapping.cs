using Granit.Domain;

namespace Granit.Customers.Domain;

/// <summary>
/// Maps a <see cref="Customer"/> to its identifier in an external system (Stripe customer ID,
/// Mollie customer ID, Odoo <c>res.partner</c> ID, Sage customer ID, …).
/// </summary>
/// <remarks>
/// One mapping per <c>(Customer, ProviderName)</c> tuple — enforced by a unique index.
/// See <see cref="CustomerExternalProviderNames"/> for reserved provider names.
/// </remarks>
public sealed class CustomerExternalMapping : Entity
{
    private CustomerExternalMapping() { }

    /// <summary>Creates a new external mapping.</summary>
    /// <param name="id">Mapping identifier.</param>
    /// <param name="providerName">Provider key (see <see cref="CustomerExternalProviderNames"/>).</param>
    /// <param name="externalId">Provider-natural identifier (e.g., <c>cus_1234</c>).</param>
    public static CustomerExternalMapping Create(Guid id, string providerName, string externalId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        return new CustomerExternalMapping
        {
            Id = id,
            ProviderName = providerName,
            ExternalId = externalId,
        };
    }

    /// <summary>Provider key — see <see cref="CustomerExternalProviderNames"/>.</summary>
    public string ProviderName { get; private set; } = string.Empty;

    /// <summary>Provider-natural identifier (free-form string).</summary>
    public string ExternalId { get; private set; } = string.Empty;
}
