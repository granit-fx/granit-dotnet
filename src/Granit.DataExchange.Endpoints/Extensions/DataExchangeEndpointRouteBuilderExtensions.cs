using Granit.DataExchange.Endpoints.Endpoints.Export;
using Granit.DataExchange.Endpoints.Endpoints.Import;
using Granit.DataExchange.Endpoints.Internal.Export;
using Granit.DataExchange.Endpoints.Internal.Import;
using Granit.DataExchange.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
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
    /// Registers the <c>DataExchange.Import</c> authorization policy (see
    /// <see cref="ImportAuthorizationPolicy.PolicyName"/>) requiring the role
    /// configured via <see cref="DataExchangeEndpointsOptions.RequiredRole"/>.
    /// </para>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapDataExchangeEndpoints();
    ///
    /// // With a custom prefix or role:
    /// app.MapDataExchangeEndpoints(opts =>
    /// {
    ///     opts.RoutePrefix = "admin/imports";
    ///     opts.RequiredRole = "ops-team";
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
    public static RouteGroupBuilder MapDataExchangeEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<DataExchangeEndpointsOptions>? configure = null)
    {
        DataExchangeEndpointsOptions options = new();
        configure?.Invoke(options);

        // Register the named authorization policies so that endpoints can use
        // RequireAuthorization(PolicyName). This is safe to call here because
        // IOptions<AuthorizationOptions> is a singleton and is evaluated lazily
        // (before the first policy lookup at request time).
        IOptions<AuthorizationOptions>? authOptions =
            endpoints.ServiceProvider.GetService<IOptions<AuthorizationOptions>>();
        authOptions?.Value.AddPolicy(
            ImportAuthorizationPolicy.PolicyName,
            policy => policy.RequireRole(options.RequiredRole));
        authOptions?.Value.AddPolicy(
            ExportAuthorizationPolicy.PolicyName,
            policy => policy.RequireRole(options.RequiredRole));

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        // Import endpoints (listing, upload, mappings, execution, reports)
        RouteGroupBuilder importGroup = group
            .RequireAuthorization(ImportAuthorizationPolicy.PolicyName);

        importGroup.MapImportJobListEndpoints();
        importGroup.MapUploadEndpoints();
        importGroup.MapExecutionEndpoints();
        importGroup.MapReportEndpoints();

        // Export job endpoints under /export/ sub-group
        RouteGroupBuilder exportGroup = group
            .MapGroup("export")
            .RequireAuthorization(ExportAuthorizationPolicy.PolicyName);

        exportGroup.MapExportJobListEndpoints();
        exportGroup.MapExportExecutionEndpoints();

        // Shared metadata (definitions, presets) under /metadata/
        // Export-specific write operations require ExportAuthorizationPolicy
        RouteGroupBuilder metadataGroup = group
            .MapGroup("metadata");

        metadataGroup.MapExportDefinitionEndpoints();

        // Empty sub-group to isolate authorization without adding a route segment.
        // Preset endpoints already include /presets/ in their individual paths.
        RouteGroupBuilder presetGroup = metadataGroup
            .MapGroup(string.Empty)
            .RequireAuthorization(ExportAuthorizationPolicy.PolicyName);

        presetGroup.MapExportPresetEndpoints();

        return group;
    }
}
