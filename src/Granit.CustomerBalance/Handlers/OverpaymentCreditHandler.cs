using Granit.Invoicing.Events;
using Granit.MultiTenancy;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.CustomerBalance.Handlers;

/// <summary>
/// Credits overpayment surplus to the contact's balance account.
/// Delegates to <see cref="IOverpaymentCreditService"/>. Idempotent — Wolverine's
/// at-least-once delivery is therefore safe.
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
                PartyId.Create(eto.PartyId),
                eto.Currency,
                eto.OverpaymentAmount,
                eto.InvoiceId,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
