using Granit.Authorization;
using Granit.DataLookup;
using Granit.DataLookup.Endpoints.Internal;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.DataLookup.Endpoints;

/// <summary>
/// Granit module for Granit.DataLookup HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes the <c>/lookups</c> routes via
/// <see cref="Extensions.DataLookupEndpointRouteBuilderExtensions.MapGranitDataLookups"/>.
/// Validators are auto-discovered by <see cref="GranitValidationModule"/>; the
/// <c>DataLookup.Lookups.Read</c> permission is contributed automatically by
/// <see cref="Permissions.DataLookupPermissionDefinitionProvider"/>.
/// </remarks>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitDataLookupModule),
    typeof(GranitValidationModule))]
public sealed class GranitDataLookupEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
        => context.Services.AddLocalizationResource<DataLookupEndpointsLocalizationResource>();
}
