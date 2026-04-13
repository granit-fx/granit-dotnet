using Granit.DataExchange.Endpoints.Endpoints.Export;
using Granit.DataExchange.Endpoints.Endpoints.Import;
using Granit.DataExchange.Endpoints.Options;
using Granit.DataExchange.Endpoints.Permissions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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
    /// Import endpoints require the <c>DataExchange.Imports.Execute</c> permission;
    /// export endpoints require <c>DataExchange.Exports.Execute</c>.
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
    /// Exposes 10 import endpoints: GET /jobs, POST /, POST /{jobId}/preview,
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
        DataExchangeEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        // Import endpoints (listing, upload, mappings, execution, reports)
        RouteGroupBuilder importGroup = group
            .MapGranitGroup("import")
            .RequireAuthorization(DataExchangePermissions.Imports.Execute);

        importGroup.MapImportJobListEndpoints();
        importGroup.MapUploadEndpoints();
        importGroup.MapExecutionEndpoints();
        importGroup.MapReportEndpoints();

        // Export job endpoints under /export/ sub-group
        RouteGroupBuilder exportGroup = group
            .MapGranitGroup("export")
            .RequireAuthorization(DataExchangePermissions.Exports.Execute);

        exportGroup.MapExportJobListEndpoints();
        exportGroup.MapExportExecutionEndpoints();

        // Shared metadata (definitions, presets) under /metadata/
        // Export-specific write operations require DataExchange.Exports.Execute
        RouteGroupBuilder metadataGroup = group
            .MapGranitGroup("metadata")
            .RequireAuthorization();

        metadataGroup.MapExportDefinitionEndpoints();

        // Empty sub-group to isolate authorization without adding a route segment.
        // Preset endpoints already include /presets/ in their individual paths.
        RouteGroupBuilder presetGroup = metadataGroup
            .MapGranitGroup(string.Empty)
            .RequireAuthorization(DataExchangePermissions.Exports.Execute);

        presetGroup.MapExportPresetEndpoints();

        return group;
    }
}
