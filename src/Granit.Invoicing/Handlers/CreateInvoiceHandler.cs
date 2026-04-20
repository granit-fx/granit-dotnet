using Granit.Invoicing.Commands;
using Granit.MultiTenancy;

namespace Granit.Invoicing.Handlers;

/// <summary>
/// Handles <see cref="CreateInvoiceCommand"/> — delegates to
/// <see cref="IInvoiceCreationService"/> for invoice creation, tax calculation,
/// and finalization.
/// </summary>
public class CreateInvoiceHandler
{
    public static async Task HandleAsync(
        CreateInvoiceCommand command,
        IInvoiceCreationService invoiceCreationService,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(command.TenantId))
        {
            await invoiceCreationService.CreateAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }
}
