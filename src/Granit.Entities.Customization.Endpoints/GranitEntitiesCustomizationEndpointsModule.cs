using Granit.Auditing;
using Granit.Authorization;
using Granit.Entities.Customization.Endpoints.Internal;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Entities.Customization.Endpoints;

/// <summary>
/// Granit module for the entities-customization HTTP endpoints — exposes the
/// per-tenant Layer 1 GET / PUT / DELETE per ADR-053.
/// </summary>
/// <remarks>
/// Map endpoints in your application:
/// <code>app.MapGranitEntitiesCustomization();</code>
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuditingModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitEntitiesCustomizationModule),
    typeof(GranitEntitiesModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule))]
public sealed class GranitEntitiesCustomizationEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddScoped<DescriptorDeltaValidator>();
        context.Services.TryAddScoped<EntityCustomizationAuditWriter>();
    }
}
