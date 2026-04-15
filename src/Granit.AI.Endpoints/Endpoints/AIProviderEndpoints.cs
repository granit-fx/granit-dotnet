using Granit.AI.Endpoints.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.AI.Endpoints.Endpoints;

internal static class AIProviderEndpoints
{
    internal static RouteGroupBuilder MapProviderEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/providers", ListProvidersAsync)
            .WithName("ListAIProviders")
            .WithSummary("Lists all registered AI providers.")
            .WithDescription(
                "Returns every AI provider registered via dependency injection, "
                + "along with an indication of whether each provider supports chat and embedding capabilities.")
            .Produces<IReadOnlyList<AIProviderResponse>>();

        group.MapGet("/providers/{providerName}/models", ListProviderModelsAsync)
            .WithName("ListAIProviderModels")
            .WithSummary("Lists the models available from a specific AI provider.")
            .WithDescription(
                "Returns the models currently available from the specified provider. "
                + "For cloud providers (OpenAI, AzureOpenAI) this is a static catalog. "
                + "For local providers (Ollama) this queries the server for installed models. "
                + "Returns 404 if the provider is not registered, or 502 if the provider's model listing fails.")
            .Produces<IReadOnlyList<AIProviderModelResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return group;
    }

    private static async Task<Ok<IReadOnlyList<AIProviderResponse>>> ListProvidersAsync(
        [FromServices] IEnumerable<IAIProviderFactory> providerFactories,
        CancellationToken cancellationToken)
    {
        var providers = new List<AIProviderResponse>();

        foreach (IAIProviderFactory factory in providerFactories)
        {
            bool supportsChat = false;
            bool supportsEmbeddings = false;

            if (factory is IAIModelCatalog catalog)
            {
                IReadOnlyList<AIModelInfo> models = await catalog
                    .GetAvailableModelsAsync(cancellationToken)
                    .ConfigureAwait(false);

                supportsChat = models.Any(m => m.Capabilities.Chat);
                supportsEmbeddings = models.Any(m => m.Capabilities.Embeddings);
            }

            providers.Add(new AIProviderResponse(factory.ProviderName, supportsChat, supportsEmbeddings));
        }

        return TypedResults.Ok<IReadOnlyList<AIProviderResponse>>(providers);
    }

    private static async Task<Results<Ok<IReadOnlyList<AIProviderModelResponse>>, ProblemHttpResult>> ListProviderModelsAsync(
        string providerName,
        [FromServices] IEnumerable<IAIProviderFactory> providerFactories,
        CancellationToken cancellationToken)
    {
        IAIProviderFactory? factory = providerFactories
            .FirstOrDefault(p => string.Equals(p.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));

        if (factory is null)
        {
            return TypedResults.Problem(
                detail: $"Provider '{providerName}' is not registered.",
                statusCode: StatusCodes.Status404NotFound);
        }

        if (factory is not IAIModelCatalog catalog)
        {
            return TypedResults.Problem(
                detail: $"Provider '{providerName}' does not support model discovery.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        IReadOnlyList<AIModelInfo> models;
        try
        {
            models = await catalog.GetAvailableModelsAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return TypedResults.Problem(
                detail: $"Failed to retrieve models from provider '{providerName}'.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        IReadOnlyList<AIProviderModelResponse> response = models
            .Select(m => new AIProviderModelResponse(m.Id, m.DisplayName, m.Capabilities, m.MaxContextTokens))
            .ToList();

        return TypedResults.Ok(response);
    }
}
