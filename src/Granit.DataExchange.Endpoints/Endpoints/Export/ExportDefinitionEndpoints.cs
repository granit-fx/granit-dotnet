using Granit.DataExchange.Endpoints.Dtos.Export;
using Granit.DataExchange.Endpoints.Dtos.Import;
using Granit.DataExchange.Endpoints.Internal.Export;
using Granit.DataExchange.Endpoints.Internal.Import;
using Granit.DataExchange.Export;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.Endpoints.Endpoints.Export;

/// <summary>
/// Export definition listing and field introspection endpoints.
/// </summary>
internal static class ExportDefinitionEndpoints
{
    /// <summary>
    /// Registers GET /definitions, GET /definitions/{name}/fields onto the given route group.
    /// </summary>
    internal static RouteGroupBuilder MapExportDefinitionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/definitions", ListDefinitionsAsync)
            .WithName("ListExportDefinitions")
            .WithSummary("Lists all registered export definitions.")
            .WithDescription("Returns all export definitions registered by application modules. Each definition describes an exportable dataset, its supported output formats, and metadata. Use the fields endpoint to discover selectable columns for a specific definition.")
            .Produces<IReadOnlyList<ExportDefinitionResponse>>();

        group.MapGet("/definitions/{name}/fields", GetFieldsAsync)
            .WithName("GetExportDefinitionFields")
            .WithSummary("Returns the available fields for a given export definition.")
            .WithDescription("Returns the list of selectable fields for the named export definition — each with its property name, display label, and data type. Use this to populate a field picker UI before creating an export job. Returns 404 if the definition name is not registered.")
            .Produces<IReadOnlyList<ExportFieldResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static Ok<IReadOnlyList<ExportDefinitionResponse>> ListDefinitionsAsync(
        [FromServices] IServiceProvider serviceProvider)
    {
        IEnumerable<IExportDefinitionDescriptor> descriptors =
            serviceProvider.GetServices<IExportDefinitionDescriptor>();

        IReadOnlyList<ExportDefinitionResponse> response = descriptors
            .Select(ExportDefinitionResponse.FromDescriptor)
            .ToList()
            .AsReadOnly();

        return TypedResults.Ok(response);
    }

    private static Results<Ok<IReadOnlyList<ExportFieldResponse>>, NotFound> GetFieldsAsync(
        string name,
        [FromServices] IServiceProvider serviceProvider)
    {
        IExportDefinitionDescriptor? descriptor =
            ExportDefinitionResolver.FindByName(serviceProvider, name);
        if (descriptor is null)
        {
            return TypedResults.NotFound();
        }

        IReadOnlyList<ExportFieldResponse> fields = descriptor.GetFields()
            .Select(ExportFieldResponse.FromDescriptor)
            .ToList()
            .AsReadOnly();

        return TypedResults.Ok(fields);
    }
}
