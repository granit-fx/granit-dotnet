using Granit.Auditing.Endpoints.Internal;
using Granit.Auditing.Endpoints.Options;
using Granit.Auditing.Endpoints.Workspaces;
using Granit.Authorization;
using Granit.Http.ApiDocumentation;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.QueryEngine.Endpoints;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Auditing.Endpoints;

/// <summary>
/// Granit module for audit log read-only Minimal API endpoints.
/// </summary>
/// <remarks>
/// <para>
/// This module does not auto-map routes. The host application must call
/// <c>app.MapGranitAuditing()</c> in the pipeline configuration.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAuditingModule),
    typeof(GranitAuthorizationModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitQueryEngineEndpointsModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitAuditingEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<AuditingEndpointsLocalizationResource>();
        context.Services.AddFeatureProvider<AuditingFeatureProvider>();

        context.Services
            .AddOptions<AuditingEndpointsOptions>()
            .BindConfiguration(AuditingEndpointsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        context.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ISchemaExampleProvider, AuditingSchemaExampleProvider>());
    }
}
