using Granit.Http.ApiDocumentation;
using Granit.Modularity;
using Granit.Validation.Endpoints.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Validation.Endpoints;

/// <summary>
/// Granit module for server-side single-field validation HTTP endpoints.
/// </summary>
/// <remarks>
/// Exposes endpoints for validating individual field values against registered
/// <see cref="ServerValidation.IServerValidator"/> instances, enabling real-time
/// (debounced) validation of complex rules (IBAN, SIREN, VAT, etc.) without
/// submitting the entire form.
/// </remarks>
[DependsOn(
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitValidationModule))]
public sealed class GranitValidationEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddSingleton<ValidationMetrics>();
}
