using Granit.Core.Modularity;
using Granit.Http.ApiDocumentation;
using Granit.Validation;

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
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitNotificationsModule),
    typeof(GranitValidationModule))]
public sealed class GranitNotificationsEndpointsModule : GranitModule;

