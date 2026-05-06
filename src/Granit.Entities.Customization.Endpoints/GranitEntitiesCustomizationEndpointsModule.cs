using Granit.Auditing;
using Granit.Authorization;
using Granit.Entities.Customization.Endpoints.Internal;
using Granit.Entities.Endpoints;
using Granit.Entities.Endpoints.Internal;
using Granit.Guids;
using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Entities.Customization.Endpoints;

/// <summary>
/// Granit module for the entities-customization HTTP endpoints — exposes the
/// per-tenant Layer 1 GET / PUT / DELETE per ADR-053. Loading this module also
/// replaces the manifest endpoint's no-op
/// <see cref="IManifestCustomizationApplier"/> with the real
/// <see cref="EntityCustomizationManifestApplier"/> so subsequent manifest
/// reads honour the tenant's customization deltas (B4).
/// </summary>
/// <remarks>
/// Map endpoints in your application:
/// <code>app.MapGranitEntitiesCustomization();</code>
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuditingModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitEntitiesAbstractionsModule),
    typeof(GranitEntitiesCustomizationModule),
    typeof(GranitEntitiesEndpointsModule),
    typeof(GranitEntitiesModule),
    typeof(GranitGuidsModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule))]
public sealed class GranitEntitiesCustomizationEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddScoped<DescriptorDeltaValidator>();
        context.Services.TryAddScoped<EntityCustomizationAuditWriter>();
        context.Services.Replace(
            ServiceDescriptor.Scoped<IManifestCustomizationApplier, EntityCustomizationManifestApplier>());
    }
}
