using Granit.AI.Endpoints.Dtos;
using Granit.AI.Endpoints.Internal;
using Granit.AI.Exceptions;
using Granit.AI.Workspaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;

namespace Granit.AI.Endpoints.Endpoints;

internal static class AIEmbeddingEndpoints
{
    internal static RouteGroupBuilder MapEmbeddingEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/embeddings/{workspaceName}", GenerateAsync)
            .WithName("AIGenerateEmbeddings")
            .WithSummary("Generates vector embeddings for input texts using the specified workspace.")
            .WithDescription(
                "Sends the input texts to the embedding model configured for the workspace and returns "
                + "float vectors for each input. Returns 404 if the workspace does not exist, "
                + "or 502 if the provider is unavailable.")
            .Produces<AIEmbeddingResponse>()
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return group;
    }

    private static async Task<Results<Ok<AIEmbeddingResponse>, ProblemHttpResult>> GenerateAsync(
        string workspaceName,
        AIEmbeddingRequest request,
        [FromServices] IAIEmbeddingGeneratorFactory embeddingFactory,
        [FromServices] IAIWorkspaceProvider workspaceProvider,
        CancellationToken cancellationToken)
    {
        AIWorkspace? workspace = await workspaceProvider
            .GetAsync(workspaceName, cancellationToken)
            .ConfigureAwait(false);

        if (workspace is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        IEmbeddingGenerator<string, Embedding<float>> generator;
        try
        {
            generator = await embeddingFactory
                .CreateAsync(workspaceName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is AIWorkspaceNotFoundException or AIProviderNotRegisteredException)
        {
            return AIProviderExceptionMapper.MapException(ex);
        }

        GeneratedEmbeddings<Embedding<float>> embeddings;
        try
        {
            embeddings = await generator
                .GenerateAsync(request.Inputs.ToList(), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || cancellationToken.IsCancellationRequested)
        {
            return AIProviderExceptionMapper.MapException(ex);
        }

        var data = embeddings
            .Select((e, i) => new AIEmbeddingDataResponse(i, e.Vector.ToArray().ToList()))
            .ToList();

        // The usage record itself is stamped by the factory-applied middleware; the endpoint
        // only surfaces the token count to the client.
        AIEmbeddingUsageResponse? usageResponse = embeddings.Usage is not null
            ? new AIEmbeddingUsageResponse((int)(embeddings.Usage.InputTokenCount ?? 0))
            : null;

        return TypedResults.Ok(new AIEmbeddingResponse(workspaceName, workspace.Model, data, usageResponse));
    }
}
