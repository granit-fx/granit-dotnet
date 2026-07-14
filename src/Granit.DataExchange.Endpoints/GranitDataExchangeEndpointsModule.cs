using Granit.Authorization;
using Granit.DataExchange.Endpoints.Internal;
using Granit.DataExchange.Endpoints.Options;
using Granit.DataExchange.Endpoints.Workspaces;
using Granit.Http.ApiDocumentation;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.Validation;
using Granit.Workspaces;
using Granit.Workspaces.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.DataExchange.Endpoints;

/// <summary>
/// Granit module for data import HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes import management routes via
/// <see cref="Extensions.DataExchangeEndpointRouteBuilderExtensions.MapGranitDataExchange"/>.
/// Requires both <see cref="GranitDataExchangeModule"/> (pipeline infrastructure)
/// and <see cref="GranitAuthorizationModule"/> (permission policy enforcement).
/// Permission definition providers are auto-discovered by <c>GranitAuthorizationModule</c>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitDataExchangeModule),
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule),
    typeof(GranitWorkspacesAbstractionsModule))]
public sealed class GranitDataExchangeEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddLocalizationResource<DataExchangeEndpointsLocalizationResource>();
        context.Services.AddFeatureProvider<DataExchangeFeatureProvider>();

        context.Services
            .AddOptions<DataExchangeEndpointsOptions>()
            .BindConfiguration(DataExchangeEndpointsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        context.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ISchemaExampleProvider, DataExchangeSchemaExampleProvider>());
    }
}
