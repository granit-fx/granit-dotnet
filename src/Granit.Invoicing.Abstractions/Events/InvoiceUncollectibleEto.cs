using Granit.Events;

namespace Granit.Invoicing.Events;

/// <summary>Published when an invoice is marked as uncollectible (bad debt).</summary>
public sealed record InvoiceUncollectibleEto(Guid InvoiceId, Guid TenantId) : IIntegrationEvent;
