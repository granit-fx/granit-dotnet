using Granit.Identity.Notifications.Extensions;
using Granit.Identity.Notifications.Internal;
using Granit.Modularity;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.Templating;
using Granit.Templating.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Identity.Notifications;

/// <summary>
/// Granit module for identity notifications: the identity-backed recipient resolver plus the
/// user-session security alerts. Provides a default <c>IRecipientResolver</c> resolving recipient
/// contact information from the Identity module — covering local (OpenIddict) and every federated
/// provider through a single adapter — and routes <see cref="SuspiciousUserSessionDetectedEto"/> to
/// users as a brand-neutral "suspicious sign-in" security alert email (location and plain-language
/// reason, never the raw IP).
/// </summary>
/// <remarks>
/// The recipient resolver is registered with <c>TryAddScoped</c>, so an application that supplies its
/// own <c>IRecipientResolver</c> keeps full control.
/// </remarks>
[DependsOn(
    typeof(GranitIdentityModule),
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitTemplatingModule))]
public sealed class GranitIdentityNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitIdentityRecipientResolver();

        // Ship the embedded HTML templates (suspicious sign-in + new-session review, EN/FR baseline).
        // Apps can override any of them at runtime through the Granit.Templating admin API.
        context.Services.AddEmbeddedTemplates(typeof(GranitIdentityNotificationsModule).Assembly);

        // Layout glob — the host application registers the actual `Layout.Email` template; if absent,
        // templates render without a layout (warning logged, no crash).
        context.Services.AddTemplateLayout("identity.*", "Layout.Email");

        context.Services.AddSingleton<INotificationDefinitionProvider, IdentityUserSessionNotificationDefinitionProvider>();

        context.Services.AddOptions<Options.IdentityUserSessionNotificationOptions>()
            .BindConfiguration(Options.IdentityUserSessionNotificationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
