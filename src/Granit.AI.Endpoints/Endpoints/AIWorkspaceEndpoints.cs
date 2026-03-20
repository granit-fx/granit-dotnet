using Granit.AI.Endpoints.Dtos;
using Granit.AI.Workspaces;
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
            .WithSummary("List all AI workspaces (system and dynamic)");

        group.MapGet("/workspaces/{name}", GetByNameAsync)
            .WithName("GetAIWorkspace")
            .WithSummary("Get an AI workspace by name");

        group.MapPost("/workspaces", CreateAsync)
            .WithName("CreateAIWorkspace")
            .WithSummary("Create a new dynamic AI workspace");

        group.MapPut("/workspaces/{name}", UpdateAsync)
            .WithName("UpdateAIWorkspace")
            .WithSummary("Update an existing dynamic AI workspace");

        group.MapDelete("/workspaces/{name}", DeleteAsync)
            .WithName("DeleteAIWorkspace")
            .WithSummary("Delete a dynamic AI workspace");

        return group;
    }

    private static async Task<Ok<AIWorkspaceListResponse>> ListAllAsync(
        [FromServices] IAIWorkspaceProvider provider,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AIWorkspace> workspaces = await provider.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var items = workspaces
            .Select(MapToResponse)
            .ToList();

        return TypedResults.Ok(new AIWorkspaceListResponse(items, items.Count));
    }

    private static async Task<Results<Ok<AIWorkspaceResponse>, NotFound>> GetByNameAsync(
        string name,
        [FromServices] IAIWorkspaceProvider provider,
        CancellationToken cancellationToken)
    {
        AIWorkspace? workspace = await provider.GetAsync(name, cancellationToken).ConfigureAwait(false);

        if (workspace is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(MapToResponse(workspace));
    }

    private static async Task<Results<Created<AIWorkspaceResponse>, ProblemHttpResult>> CreateAsync(
        AIWorkspaceCreateRequest request,
        [FromServices] IAIWorkspaceManager manager,
        [FromServices] IAIWorkspaceProvider provider,
        CancellationToken cancellationToken)
    {
        AIWorkspace workspace = new()
        {
            Name = request.Name,
            Provider = request.Provider,
            Model = request.Model,
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
                detail: $"A workspace named '{request.Name}' already exists.",
                statusCode: StatusCodes.Status409Conflict);
        }

        AIWorkspace? created = await provider.GetAsync(request.Name, cancellationToken).ConfigureAwait(false);
        return TypedResults.Created($"/workspaces/{request.Name}", MapToResponse(created!));
    }

    private static async Task<Results<Ok<AIWorkspaceResponse>, NotFound, ProblemHttpResult>> UpdateAsync(
        string name,
        AIWorkspaceUpdateRequest request,
        [FromServices] IAIWorkspaceManager manager,
        [FromServices] IAIWorkspaceProvider provider,
        CancellationToken cancellationToken)
    {
        AIWorkspace? existing = await provider.GetAsync(name, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return TypedResults.NotFound();
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
            SystemPrompt = request.SystemPrompt,
            Temperature = request.Temperature,
            MaxOutputTokens = request.MaxOutputTokens,
            IsActive = request.IsActive,
        };

        await manager.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);

        AIWorkspace? refreshed = await provider.GetAsync(name, cancellationToken).ConfigureAwait(false);
        return TypedResults.Ok(MapToResponse(refreshed!));
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> DeleteAsync(
        string name,
        [FromServices] IAIWorkspaceManager manager,
        [FromServices] IAIWorkspaceProvider provider,
        CancellationToken cancellationToken)
    {
        AIWorkspace? existing = await provider.GetAsync(name, cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return TypedResults.NotFound();
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

    private static AIWorkspaceResponse MapToResponse(AIWorkspace workspace) => new(
        workspace.Name,
        workspace.Provider,
        workspace.Model,
        workspace.SystemPrompt,
        workspace.Temperature,
        workspace.MaxOutputTokens,
        workspace.Kind.ToString(),
        workspace.IsActive);
}
