using Granit.Domain;

namespace Granit.Invoicing.Domain;

/// <summary>Maps an invoice to an external system (Stripe, Odoo).</summary>
public sealed class InvoiceExternalReference : Entity
{
    private InvoiceExternalReference() { }

    public static InvoiceExternalReference Create(Guid id, string providerName, string externalId) =>
        new() { Id = id, ProviderName = providerName, ExternalId = externalId };

    public string ProviderName { get; private set; } = string.Empty;
    public string ExternalId { get; private set; } = string.Empty;
}
