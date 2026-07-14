using Granit.DataExchange.Endpoints.Endpoints.Export;
using Granit.DataExchange.Endpoints.Endpoints.Import;
using Granit.DataExchange.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.DataExchange.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering data exchange endpoints.
/// </summary>
public static class DataExchangeEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the data exchange endpoints onto the given route builder.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each route declares its own least-privilege permission: read-only GET endpoints
    /// (job listing, status, report, correction file, export definitions/fields, preset
    /// listing, download) require <c>DataExchange.Imports.Read</c> / <c>DataExchange.Exports.Read</c>;
    /// mutating endpoints (upload, mapping confirmation, execution, dry-run, cancellation,
    /// export job creation, preset save/delete) require <c>DataExchange.Imports.Execute</c> /
    /// <c>DataExchange.Exports.Execute</c>.
    /// </para>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapGranitDataExchange();
    ///
    /// // With a custom prefix:
    /// app.MapGranitDataExchange(opts =>
    /// {
    ///     opts.RoutePrefix = "admin/imports";
    /// });
    /// </code>
    /// <para>
    /// Exposes 10 import endpoints: GET /jobs, POST /jobs, POST /{jobId}/preview,
    /// PUT /{jobId}/mappings, POST /{jobId}/execute, POST /{jobId}/dry-run,
    /// GET /{jobId}, DELETE /{jobId}, GET /{jobId}/report, GET /{jobId}/correction-file.
    /// </para>
    /// <para>
    /// Export job endpoints under <c>/export/</c>:
    /// GET /jobs, POST /jobs, GET /jobs/{id}, GET /jobs/{id}/download.
    /// </para>
    /// <para>
    /// Shared metadata endpoints under <c>/metadata/</c>:
    /// GET /definitions, GET /definitions/{name}/fields,
    /// GET /presets/{definitionName}, POST /presets,
    /// DELETE /presets/{definitionName}/{presetName}.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="DataExchangeEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitDataExchange(
        this IEndpointRouteBuilder endpoints,
        Action<DataExchangeEndpointsOptions>? configure = null)
    {
        DataExchangeEndpointsOptions options = endpoints.ServiceProvider
            .GetService<IOptions<DataExchangeEndpointsOptions>>()?.Value
            ?? new DataExchangeEndpointsOptions();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        // Import endpoints (listing, upload, mappings, execution, reports).
        // Each route declares its own Imports.Read (GET) or Imports.Execute (mutation) permission.
        RouteGroupBuilder importGroup = group.MapGranitGroup("import");

        importGroup.MapImportJobListEndpoints();
        importGroup.MapUploadEndpoints();
        importGroup.MapExecutionEndpoints();
        importGroup.MapReportEndpoints();

        // Export job endpoints under /export/ sub-group.
        // Each route declares its own Exports.Read (GET) or Exports.Execute (mutation) permission.
        RouteGroupBuilder exportGroup = group.MapGranitGroup("export");

        exportGroup.MapExportJobListEndpoints();
        exportGroup.MapExportExecutionEndpoints();

        // Shared metadata (definitions, presets) under /metadata/.
        // Each route declares its own Exports.Read (GET) or Exports.Execute (mutation) permission.
        RouteGroupBuilder metadataGroup = group.MapGranitGroup("metadata");

        metadataGroup.MapExportDefinitionEndpoints();
        metadataGroup.MapExportPresetEndpoints();

        return group;
    }
}
