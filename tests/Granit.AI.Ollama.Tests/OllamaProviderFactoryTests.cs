using Granit.AI.Ollama.Internal;
using Granit.AI.Ollama.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Shouldly;

namespace Granit.AI.Ollama.Tests;

public sealed class OllamaProviderFactoryTests
{
    private static OllamaProviderFactory CreateFactory(OllamaProviderOptions? options = null)
    {
        OllamaProviderOptions opts = options ?? new OllamaProviderOptions();
        return new OllamaProviderFactory(
            new TestOptionsMonitor<OllamaProviderOptions>(opts),
            new TestHttpClientFactory(),
            TimeProvider.System);
    }

    private static AIWorkspace CreateWorkspace(string? model = "llama3.2") =>
        new()
        {
            Name = "test",
            Provider = "Ollama",
            Model = model!,
        };

    [Fact]
    public void ProviderName_IsOllama()
    {
        OllamaProviderFactory factory = CreateFactory();

        factory.ProviderName.ShouldBe("Ollama");
    }

    [Fact]
    public void CreateChatClient_WrapsOllamaApiClient()
    {
        OllamaProviderFactory factory = CreateFactory();

        IChatClient client = factory.CreateChatClient(CreateWorkspace());

        client.ShouldNotBeNull();
        client.GetService(typeof(OllamaApiClient)).ShouldNotBeNull();
    }

    [Fact]
    public void CreateEmbeddingGenerator_ReturnsOllamaApiClient()
    {
        OllamaProviderFactory factory = CreateFactory();

        IEmbeddingGenerator<string, Embedding<float>> generator =
            factory.CreateEmbeddingGenerator(CreateWorkspace("nomic-embed-text"))!;

        generator.ShouldNotBeNull();
        generator.ShouldBeOfType<OllamaApiClient>();
    }

    [Fact]
    public void CreateChatClient_NullWorkspace_ThrowsArgumentNullException()
    {
        OllamaProviderFactory factory = CreateFactory();

        Should.Throw<ArgumentNullException>(() => factory.CreateChatClient(null!));
    }

    [Fact]
    public void CreateEmbeddingGenerator_NullWorkspace_ThrowsArgumentNullException()
    {
        OllamaProviderFactory factory = CreateFactory();

        Should.Throw<ArgumentNullException>(() => factory.CreateEmbeddingGenerator(null!));
    }
}
