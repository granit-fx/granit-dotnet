using Granit.Invoicing.Events;
using Granit.MultiTenancy;

namespace Granit.CustomerBalance.Wolverine.Handlers;

/// <summary>
/// Credits overpayment surplus to the tenant's balance account.
/// Delegates to <see cref="IOverpaymentCreditService"/>.
/// </summary>
public class OverpaymentCreditHandler
{
    public static async Task HandleAsync(
        OverpaymentDetectedEto eto,
        IOverpaymentCreditService overpaymentCreditService,
        ICurrentTenant currentTenant,
        CancellationToken cancellationToken)
    {
        using (currentTenant.Change(eto.TenantId))
        {
            await overpaymentCreditService.CreditOverpaymentAsync(
                eto.TenantId,
                eto.Currency,
                eto.OverpaymentAmount,
                eto.InvoiceId,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
