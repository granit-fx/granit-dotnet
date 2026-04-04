using Granit.Invoicing.Commands;

namespace Granit.Invoicing;

/// <summary>
/// Creates draft invoices, applies tax calculation and number generation when available,
/// and persists the result. Extracted from the Wolverine handler to keep handlers thin
/// and enable reuse from endpoints or other entry points.
/// </summary>
public interface IInvoiceCreationService
{
    /// <summary>
    /// Creates a draft invoice from the given command, applies tax and numbering, and persists it.
    /// </summary>
    /// <param name="command">Invoice creation command with line items and billing details.</param>
    /// <param name="cancellationToken"></param>
    Task CreateAsync(CreateInvoiceCommand command, CancellationToken cancellationToken = default);
}
