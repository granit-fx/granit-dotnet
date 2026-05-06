using Granit.Activities.Endpoints.Options;
using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Activities.Endpoints;

/// <summary>
/// Granit module for activity HTTP endpoints — exposes the polymorphic
/// to-do CRUD + lifecycle transitions via Minimal API.
/// </summary>
/// <remarks>
/// Map endpoints in your application:
/// <code>app.MapGranitActivities();</code>
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitActivitiesModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule))]
public sealed class GranitActivitiesEndpointsModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<ActivitiesEndpointsOptions>()
            .BindConfiguration(ActivitiesEndpointsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
