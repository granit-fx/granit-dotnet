using Granit.AI.Ollama.Internal;
using Granit.AI.Ollama.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Shouldly;

namespace Granit.AI.Ollama.Tests;

public sealed class OllamaProviderFactoryAdditionalTests
{
    private static AIWorkspace CreateWorkspace(string? model = "llama3.2") =>
        new()
        {
            Name = "test",
            Provider = "Ollama",
            Model = model!,
        };

    private static OllamaApiClient UnwrapApiClient(IChatClient client) =>
        (OllamaApiClient)client.GetService(typeof(OllamaApiClient))!;

    [Fact]
    public async Task CreateChatClientAsync_NullModel_FallsBackToDefaultModel()
    {
        (OllamaProviderFactory factory, _, _, _) = TestFixtures.BuildFactory(
            new OllamaProviderOptions { Endpoint = "http://localhost:11434", DefaultModel = "mistral" });

        IChatClient client = await factory.CreateChatClientAsync(
            CreateWorkspace(model: null),
            TestContext.Current.CancellationToken);

        UnwrapApiClient(client).SelectedModel.ShouldBe("mistral");
    }

    [Fact]
    public async Task CreateEmbeddingGeneratorAsync_NullModel_FallsBackToDefaultModel()
    {
        (OllamaProviderFactory factory, _, _, _) = TestFixtures.BuildFactory(
            new OllamaProviderOptions { Endpoint = "http://localhost:11434", DefaultModel = "nomic-embed-text" });

        var generator = (OllamaApiClient)(await factory.CreateEmbeddingGeneratorAsync(
            CreateWorkspace(model: null),
            TestContext.Current.CancellationToken))!;

        generator.SelectedModel.ShouldBe("nomic-embed-text");
    }

    [Fact]
    public async Task CreateChatClientAsync_ExplicitModel_UsesWorkspaceModel()
    {
        (OllamaProviderFactory factory, _, _, _) = TestFixtures.BuildFactory(
            new OllamaProviderOptions { Endpoint = "http://localhost:11434", DefaultModel = "mistral" });

        IChatClient client = await factory.CreateChatClientAsync(
            CreateWorkspace("phi3"),
            TestContext.Current.CancellationToken);

        UnwrapApiClient(client).SelectedModel.ShouldBe("phi3");
    }

    [Fact]
    public async Task CreateChatClientAsync_CustomEndpoint_UsesConfiguredEndpoint()
    {
        // Host endpoint is permissive — it can include private IPs (operator-trusted).
        (OllamaProviderFactory factory, _, _, _) = TestFixtures.BuildFactory(
            new OllamaProviderOptions { Endpoint = "http://gpu-server.internal:11434", DefaultModel = "llama3.2" });

        IChatClient client = await factory.CreateChatClientAsync(
            CreateWorkspace(),
            TestContext.Current.CancellationToken);

        UnwrapApiClient(client).ShouldNotBeNull();
    }
}
