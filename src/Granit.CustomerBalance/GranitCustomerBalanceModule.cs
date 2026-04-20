using Granit.CustomerBalance.Extensions;
using Granit.Guids;
using Granit.Modularity;
using Granit.Timing;

namespace Granit.CustomerBalance;

/// <summary>
/// Granit module for per-tenant, per-currency credit balance with append-only ledger.
/// </summary>
/// <remarks>
/// <para>
/// Manages accounting credits (promotional, overpayment surplus, manual adjustments)
/// applied before PSP charges. Not a payment method or e-money system.
/// </para>
/// <para>
/// Replaces the default <c>IInvoicePrePaymentProcessor</c> to deduct available credit
/// before PSP charges. Delegates credit application on invoices through
/// <c>IInvoiceCreditApplier</c> so this module only needs <c>Granit.Invoicing.Abstractions</c>
/// (light contract), not the full <c>Granit.Invoicing</c> package.
/// Add <c>Granit.CustomerBalance.EntityFrameworkCore</c> for persistence and
/// <c>Granit.CustomerBalance.BackgroundJobs</c> for credit expiration.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitGuidsModule),
    typeof(GranitTimingModule))]
public sealed class GranitCustomerBalanceModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitCustomerBalance();
}
