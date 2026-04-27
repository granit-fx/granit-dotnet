using Granit.Modularity;
using Granit.Notifications;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.CustomerBalance.Notifications;

/// <summary>
/// Granit module for the customer balance notification bridge.
/// Routes <c>BalanceCreditedEto</c> and <c>CreditExpiredEto</c> integration events to
/// users (and tenant administrators when the event lacks a recipient) via
/// <c>Granit.Notifications</c>, so they learn about credits granted to their balance and
/// — most importantly — about credits approaching or past their expiration date before
/// any value is silently lost.
/// </summary>
[DependsOn(
    typeof(GranitCustomerBalanceModule),
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitCustomerBalanceNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Ship the embedded HTML templates for every customer-balance notification.
        // Apps can override any of them at runtime through the Granit.Templating admin
        // API (the DB-backed resolver runs at higher priority than the embedded one).
        context.Services.AddEmbeddedTemplates(typeof(GranitCustomerBalanceNotificationsModule).Assembly);

        // Layout glob — covers customer-balance.credit_granted and
        // customer-balance.credit_expired. The host application registers the actual
        // `Layout.Email` template; if absent, templates render without layout (warning
        // logged, no crash).
        context.Services.AddTemplateLayout("customer-balance.*", "Layout.Email");
    }
}
