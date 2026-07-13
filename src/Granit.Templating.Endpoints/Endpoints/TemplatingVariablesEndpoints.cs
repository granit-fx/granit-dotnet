using System.Reflection;
using Granit.Templating.Endpoints.Dtos;
using Granit.Templating.Endpoints.Internal;
using Granit.Templating.Endpoints.Permissions;
using Granit.Templating.GlobalContext;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Templating.Endpoints.Endpoints;

/// <summary>
/// Variables introspection Minimal API endpoint for templates:
/// returns global, model, and enriched variables available for autocompletion.
/// </summary>
internal static class TemplatingVariablesEndpoints
{
    /// <summary>Maps the template variables endpoint to the given route group.</summary>
    public static RouteGroupBuilder MapTemplatingVariablesEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{name}/variables", HandleGetVariablesAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Read)
             .WithName("GetTemplateVariables")
             .WithSummary("Returns all available template variables (global, model, enriched) for autocompletion.")
             .WithDescription("Returns all variables available for use in the template: global variables (application-wide), model variables (bound to the template's entity type), and enriched variables (computed at render time). Used to power autocompletion in the template editor.")
             .Produces<TemplateVariablesResponse>()
             .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
    }

    // -------------------------------------------------------------------------
    // GET /{name}/variables — Available template variables
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<TemplateVariablesResponse>, ProblemHttpResult>> HandleGetVariablesAsync(
        HttpContext context,
        string name)
    {
        ProblemHttpResult? nameError = TemplatingResponseMapper.ValidateTemplateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        // Global variables — discovered by reflecting on ITemplateGlobalContext.ResolveAsync() return types
        var globalContexts =
            context.RequestServices.GetServices<ITemplateGlobalContext>().ToList();

        List<TemplateVariableItemResponse> globalVariables = [];
        foreach (ITemplateGlobalContext globalContext in globalContexts)
        {
            object resolved = await globalContext.ResolveAsync(context.RequestAborted).ConfigureAwait(false);
            Type resolvedType = resolved.GetType();

            foreach (PropertyInfo property in resolvedType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                string variableName = $"{globalContext.ContextName}.{TemplatingResponseMapper.ToSnakeCase(property.Name)}";
                string typeName = TemplatingResponseMapper.MapClrTypeName(property.PropertyType);
                globalVariables.Add(new TemplateVariableItemResponse(variableName, typeName, null));
            }
        }

        // Model and enriched variables are not yet discoverable at runtime.
        // TemplateType<TData> instances are static singletons, not registered in DI.
        // A future ITemplateTypeRegistry could enable model variable introspection.
        var response = new TemplateVariablesResponse(
            globalVariables,
            ModelVariables: [],
            EnrichedVariables: []);

        return TypedResults.Ok(response);
    }
}
