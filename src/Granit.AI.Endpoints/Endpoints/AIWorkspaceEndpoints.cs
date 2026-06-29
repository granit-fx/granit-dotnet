using Granit.AI.Endpoints.Dtos;
using Granit.AI.Workspaces;
using Granit.Authorization.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.AI.Endpoints.Endpoints;

internal static class AIWorkspaceEndpoints
{
    internal static RouteGroupBuilder MapWorkspaceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/workspaces", ListAllAsync)
            .WithName("ListAIWorkspaces")
            .WithSummary("Lists all AI workspaces, both system and dynamic.")
            .WithDescription(
                "Returns every registered workspace with its provider, model, and configuration. "
                + "System workspaces are defined in configuration; dynamic workspaces are user-created.")
            .Produces<AIWorkspaceListResponse>()
            .AllowHostAccess();

        group.MapGet("/workspaces/{name}", GetByNameAsync)
            .WithName("GetAIWorkspace")
            .WithSummary("Returns an AI workspace by its name.")
            .WithDescription(
                "Fetches the full configuration of a single workspace including provider, model, "
                + "system prompt, and active status. Returns 404 if no workspace matches the name.")
            .Produces<AIWorkspaceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .AllowHostAccess();

        group.MapPost("/workspaces", CreateAsync)
            .WithName("CreateAIWorkspace")
            .WithSummary("Creates a new dynamic AI workspace.")
            .WithDescription(
                "Registers a new user-defined workspace with the specified provider and model configuration. "
                + "Returns 409 if a workspace with the same name already exists.")
            .Produces<AIWorkspaceResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/workspaces/{name}", UpdateAsync)
            .WithName("UpdateAIWorkspace")
            .WithSummary("Updates an existing dynamic AI workspace.")
            .WithDescription(
                "Replaces the provider, model, and prompt configuration of a dynamic workspace. "
                + "System workspaces cannot be modified and return 422. "
                + "Returns 404 if the workspace does not exist.")
            .Produces<AIWorkspaceResponse>()
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/workspaces/{name}", DeleteAsync)
            .WithName("DeleteAIWorkspace")
            .WithSummary("Deletes a dynamic AI workspace.")
            .WithDescription(
                "Permanently removes a user-created workspace. "
                + "System workspaces cannot be deleted and return 422. "
                + "Returns 404 if the workspace does not exist.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return group;
    }

    private static async Task<Ok<AIWorkspaceListResponse>> ListAllAsync(
        [FromServices] IAIWorkspaceProvider provider,
        [FromServices] IAIWorkspaceCapabilityResolver capabilityResolver,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AIWorkspace> workspaces = await provider.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Batch-resolve capabilities grouped by provider to avoid redundant catalog calls.
        Dictionary<(string Provider, string Model), AIModelCapabilities?> capabilitiesByProviderModel = [];

        foreach (AIWorkspace workspace in workspaces)
        {
            (string Provider, string Model) key = (workspace.Provider, workspace.Model);
            if (!capabilitiesByProviderModel.ContainsKey(key))
            {
                capabilitiesByProviderModel[key] = await capabilityResolver
                    .ResolveAsync(workspace.Provider, workspace.Model, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var items = workspaces
            .Select(ws => MapToResponse(ws, capabilitiesByProviderModel[(ws.Provider, ws.Model)]))
            .ToList();

        return TypedResults.Ok(new AIWorkspaceListResponse(items, items.Count));
    }

    private static async Task<Results<Ok<AIWorkspaceResponse>, ProblemHttpResult>> GetByNameAsync(
        string name,
        [FromServices] IAIWorkspaceProvider provider,
        [FromServices] IAIWorkspaceCapabilityResolver capabilityResolver,
        CancellationToken cancellationToken)
    {
        AIWorkspace? workspace = await provider.GetAsync(name, cancellationToken).ConfigureAwait(false);

        if (workspace is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        AIModelCapabilities? capabilities = await capabilityResolver
            .ResolveAsync(workspace.Provider, workspace.Model, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(MapToResponse(workspace, capabilities));
    }

    private static async Task<Results<Created<AIWorkspaceResponse>, ProblemHttpResult>> CreateAsync(
        AIWorkspaceCreateRequest request,
        [FromServices] IAIWorkspaceManager manager,
        [FromServices] IAIWorkspaceProvider provider,
        [FromServices] IAIWorkspaceCapabilityResolver capabilityResolver,
        CancellationToken cancellationToken)
    {
        AIWorkspace workspace = new()
        {
            Key = request.Key,
            Provider = request.Provider,
            Model = request.Model,
            DisplayName = request.DisplayName,
            SystemPrompt = request.SystemPrompt,
            Temperature = request.Temperature,
            MaxOutputTokens = request.MaxOutputTokens,
            Kind = AIWorkspaceKind.Dynamic,
        };

        try
        {
            await manager.CreateAsync(workspace, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return TypedResults.Problem(
                detail: $"A workspace with key '{request.Key}' already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        AIWorkspace? created = await provider.GetAsync(request.Key, cancellationToken).ConfigureAwait(false);

        AIModelCapabilities? capabilities = await capabilityResolver
            .ResolveAsync(request.Provider, request.Model, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Created($"/workspaces/{request.Key}", MapToResponse(created!, capabilities));
    }

    private static async Task<Results<Ok<AIWorkspaceResponse>, ProblemHttpResult>> UpdateAsync(
        string name,
        AIWorkspaceUpdateRequest request,
        [FromServices] IAIWorkspaceManager manager,
        [FromServices] IAIWorkspaceProvider provider,
        [FromServices] IAIWorkspaceCapabilityResolver capabilityResolver,
        CancellationToken cancellationToken)
    {
        AIWorkspace? existing = await provider.GetAsync(name, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        if (existing.Kind == AIWorkspaceKind.System)
        {
            return TypedResults.Problem(
                detail: $"System workspace '{name}' cannot be modified.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        AIWorkspace updated = existing with
        {
            Provider = request.Provider,
            Model = request.Model,
            DisplayName = request.DisplayName,
            SystemPrompt = request.SystemPrompt,
            Temperature = request.Temperature,
            MaxOutputTokens = request.MaxOutputTokens,
            Activated = request.Activated,
        };

        await manager.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);

        AIWorkspace? refreshed = await provider.GetAsync(name, cancellationToken).ConfigureAwait(false);

        AIModelCapabilities? capabilities = await capabilityResolver
            .ResolveAsync(request.Provider, request.Model, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(MapToResponse(refreshed!, capabilities));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        string name,
        [FromServices] IAIWorkspaceManager manager,
        [FromServices] IAIWorkspaceProvider provider,
        CancellationToken cancellationToken)
    {
        AIWorkspace? existing = await provider.GetAsync(name, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        if (existing.Kind == AIWorkspaceKind.System)
        {
            return TypedResults.Problem(
                detail: $"System workspace '{name}' cannot be deleted.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        await manager.DeleteAsync(name, cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static AIWorkspaceResponse MapToResponse(AIWorkspace workspace, AIModelCapabilities? capabilities) => new(
        workspace.Key,
        workspace.Provider,
        workspace.Model,
        workspace.DisplayName,
        workspace.SystemPrompt,
        workspace.Temperature,
        workspace.MaxOutputTokens,
        workspace.Kind,
        workspace.Activated,
        capabilities);
}
