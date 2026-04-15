using Granit.AI.Ollama.Internal;
using Granit.AI.Ollama.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Shouldly;

namespace Granit.AI.Ollama.Tests;

public sealed class OllamaProviderFactoryAdditionalTests
{
    private static OllamaProviderFactory CreateFactory(OllamaOptions? options = null)
    {
        OllamaOptions opts = options ?? new OllamaOptions();
        return new OllamaProviderFactory(Microsoft.Extensions.Options.Options.Create(opts), TimeProvider.System);
    }

    private static AIWorkspace CreateWorkspace(string? model = "llama3.2") =>
        new()
        {
            Name = "test",
            Provider = "Ollama",
            Model = model!,
        };

    [Fact]
    public void CreateChatClient_NullModel_FallsBackToDefaultModel()
    {
        var options = new OllamaOptions { DefaultModel = "mistral" };
        OllamaProviderFactory factory = CreateFactory(options);

        var client = (OllamaApiClient)factory.CreateChatClient(CreateWorkspace(model: null));

        client.SelectedModel.ShouldBe("mistral");
    }

    [Fact]
    public void CreateEmbeddingGenerator_NullModel_FallsBackToDefaultModel()
    {
        var options = new OllamaOptions { DefaultModel = "nomic-embed-text" };
        OllamaProviderFactory factory = CreateFactory(options);

        var generator = (OllamaApiClient)factory.CreateEmbeddingGenerator(CreateWorkspace(model: null));

        generator.SelectedModel.ShouldBe("nomic-embed-text");
    }

    [Fact]
    public void CreateChatClient_ExplicitModel_UsesWorkspaceModel()
    {
        var options = new OllamaOptions { DefaultModel = "mistral" };
        OllamaProviderFactory factory = CreateFactory(options);

        var client = (OllamaApiClient)factory.CreateChatClient(CreateWorkspace("phi3"));

        client.SelectedModel.ShouldBe("phi3");
    }

    [Fact]
    public void CreateChatClient_CustomEndpoint_UsesConfiguredEndpoint()
    {
        var options = new OllamaOptions { Endpoint = "http://gpu-server:11434" };
        OllamaProviderFactory factory = CreateFactory(options);

        IChatClient client = factory.CreateChatClient(CreateWorkspace());

        client.ShouldBeOfType<OllamaApiClient>();
    }

    [Fact]
    public void CreateEmbeddingGenerator_CustomEndpoint_UsesConfiguredEndpoint()
    {
        var options = new OllamaOptions { Endpoint = "https://ollama.internal:443" };
        OllamaProviderFactory factory = CreateFactory(options);

        IEmbeddingGenerator<string, Embedding<float>> generator =
            factory.CreateEmbeddingGenerator(CreateWorkspace());

        generator.ShouldBeOfType<OllamaApiClient>();
    }
}
