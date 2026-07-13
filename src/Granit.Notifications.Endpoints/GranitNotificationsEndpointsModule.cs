using Granit.Authorization;
using Granit.Guids;
using Granit.Http.ApiDocumentation;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.Notifications.Endpoints.Internal;
using Granit.Notifications.Endpoints.Workspaces;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;

namespace Granit.Notifications.Endpoints;

/// <summary>
/// Granit module for notification HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes inbox, activity feed, preferences, subscriptions and push subscription
/// management routes via Minimal API endpoints.
/// Validators are auto-discovered by <c>GranitValidationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitGuidsModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitNotificationsModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitNotificationsEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<NotificationsEndpointsLocalizationResource>();
        context.Services.AddFeatureProvider<NotificationsFeatureProvider>();
    }
}
