using Granit.AI.Ollama.Internal;
using Granit.AI.Ollama.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Shouldly;

namespace Granit.AI.Ollama.Tests;

public sealed class OllamaProviderFactoryAdditionalTests
{
    private static OllamaProviderFactory CreateFactory(out TestOptionsMonitor<OllamaProviderOptions> monitor, OllamaProviderOptions? options = null)
    {
        OllamaProviderOptions opts = options ?? new OllamaProviderOptions();
        monitor = new TestOptionsMonitor<OllamaProviderOptions>(opts);
        return new OllamaProviderFactory(monitor, new TestHttpClientFactory(), TimeProvider.System);
    }

    private static OllamaProviderFactory CreateFactory(OllamaProviderOptions? options = null)
        => CreateFactory(out _, options);

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
    public void CreateChatClient_NullModel_FallsBackToDefaultModel()
    {
        OllamaProviderFactory factory = CreateFactory(new OllamaProviderOptions { DefaultModel = "mistral" });

        IChatClient client = factory.CreateChatClient(CreateWorkspace(model: null));

        UnwrapApiClient(client).SelectedModel.ShouldBe("mistral");
    }

    [Fact]
    public void CreateEmbeddingGenerator_NullModel_FallsBackToDefaultModel()
    {
        OllamaProviderFactory factory = CreateFactory(new OllamaProviderOptions { DefaultModel = "nomic-embed-text" });

        var generator = (OllamaApiClient)factory.CreateEmbeddingGenerator(CreateWorkspace(model: null));

        generator.SelectedModel.ShouldBe("nomic-embed-text");
    }

    [Fact]
    public void CreateChatClient_ExplicitModel_UsesWorkspaceModel()
    {
        OllamaProviderFactory factory = CreateFactory(new OllamaProviderOptions { DefaultModel = "mistral" });

        IChatClient client = factory.CreateChatClient(CreateWorkspace("phi3"));

        UnwrapApiClient(client).SelectedModel.ShouldBe("phi3");
    }

    [Fact]
    public void CreateChatClient_CustomEndpoint_UsesConfiguredEndpoint()
    {
        OllamaProviderFactory factory = CreateFactory(new OllamaProviderOptions { Endpoint = "http://gpu-server:11434" });

        IChatClient client = factory.CreateChatClient(CreateWorkspace());

        UnwrapApiClient(client).ShouldNotBeNull();
    }

    [Fact]
    public void CreateEmbeddingGenerator_CustomEndpoint_UsesConfiguredEndpoint()
    {
        OllamaProviderFactory factory = CreateFactory(new OllamaProviderOptions { Endpoint = "https://ollama.internal:443" });

        IEmbeddingGenerator<string, Embedding<float>> generator =
            factory.CreateEmbeddingGenerator(CreateWorkspace());

        generator.ShouldBeOfType<OllamaApiClient>();
    }

    [Fact]
    public void CreateChatClient_WithModelOutsideAllowlist_Throws()
    {
        OllamaProviderOptions options = new()
        {
            DefaultModel = "llama3.2",
            AllowedModels = ["llama3.2"],
        };
        OllamaProviderFactory factory = CreateFactory(options);

        Should.Throw<InvalidOperationException>(
            () => factory.CreateChatClient(CreateWorkspace("phi3")));
    }

    [Fact]
    public void CreateEmbeddingGenerator_WithModelOutsideAllowlist_Throws()
    {
        OllamaProviderOptions options = new()
        {
            DefaultModel = "llama3.2",
            AllowedModels = ["llama3.2"],
        };
        OllamaProviderFactory factory = CreateFactory(options);

        Should.Throw<InvalidOperationException>(
            () => factory.CreateEmbeddingGenerator(CreateWorkspace("phi3")));
    }

    [Fact]
    public void OptionsChange_NewEndpoint_PickedUpOnNextCall()
    {
        OllamaProviderFactory factory = CreateFactory(
            out TestOptionsMonitor<OllamaProviderOptions> monitor,
            new OllamaProviderOptions { Endpoint = "http://localhost:11434", DefaultModel = "mistral" });

        IChatClient before = factory.CreateChatClient(CreateWorkspace());
        UnwrapApiClient(before).ShouldNotBeNull();

        monitor.Set(new OllamaProviderOptions { Endpoint = "http://gpu-server:11434", DefaultModel = "mistral" });

        IChatClient after = factory.CreateChatClient(CreateWorkspace());
        UnwrapApiClient(after).ShouldNotBeNull();
    }
}
