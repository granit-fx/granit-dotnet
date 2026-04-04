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
/// Add <c>Granit.CustomerBalance.EntityFrameworkCore</c> for persistence,
/// <c>Granit.CustomerBalance.Wolverine</c> for billing integration, and
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
