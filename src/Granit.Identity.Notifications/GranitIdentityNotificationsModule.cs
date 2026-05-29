using Granit.Identity.Notifications.Extensions;
using Granit.Modularity;
using Granit.Notifications;

namespace Granit.Identity.Notifications;

/// <summary>
/// Granit module wiring the identity-backed recipient resolver. Provides a
/// default <c>IRecipientResolver</c> for the notification pipeline by resolving
/// recipient contact information from the Identity module — covering local
/// (OpenIddict) and every federated provider through a single adapter.
/// </summary>
/// <remarks>
/// Registered with <c>TryAddScoped</c>, so an application that supplies its own
/// <c>IRecipientResolver</c> keeps full control.
/// </remarks>
[DependsOn(
    typeof(GranitIdentityModule),
    typeof(GranitNotificationsAbstractionsModule))]
public sealed class GranitIdentityNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIdentityRecipientResolver();
}
