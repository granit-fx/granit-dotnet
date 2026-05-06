using Granit.Activities.Endpoints.Authorization;
using Granit.Activities.Endpoints.Options;
using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddOptions<ActivitiesEndpointsOptions>()
            .BindConfiguration(ActivitiesEndpointsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Catch-all wildcard provider — host modules contributing sensitive
        // entities replace this by registering a stricter provider for the
        // matching EntityType (VULN-102 / VULN-202).
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IActivityHostAuthorizationProvider, AllowAllActivityHostAuthorizationProvider>());
        context.Services.TryAddSingleton<ActivityHostAuthorizer>();
    }
}
