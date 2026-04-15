using Granit.AI.OpenAI.Internal;
using Granit.AI.OpenAI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Granit.AI.OpenAI.Tests;

public sealed class OpenAIProviderFactoryTests
{
    private static OpenAIProviderFactory CreateFactory(
        string apiKey = "sk-test-key-123",
        string? endpoint = null,
        string defaultModel = "gpt-4o",
        string defaultEmbeddingModel = "text-embedding-3-small")
    {
        IOptions<OpenAIProviderOptions> options = Microsoft.Extensions.Options.Options.Create(new OpenAIProviderOptions
        {
            ApiKey = apiKey,
            Endpoint = endpoint,
            DefaultModel = defaultModel,
            DefaultEmbeddingModel = defaultEmbeddingModel,
        });

        return new OpenAIProviderFactory(options, TimeProvider.System);
    }

    private static AIWorkspace CreateWorkspace(string model = "gpt-4o") =>
        new()
        {
            Name = "test-workspace",
            Provider = "OpenAI",
            Model = model,
        };

    [Fact]
    public void ProviderName_IsOpenAI()
    {
        OpenAIProviderFactory factory = CreateFactory();

        factory.ProviderName.ShouldBe("OpenAI");
    }

    [Fact]
    public void CreateChatClient_WithValidOptions_ReturnsClient()
    {
        OpenAIProviderFactory factory = CreateFactory();
        AIWorkspace workspace = CreateWorkspace();

        IChatClient client = factory.CreateChatClient(workspace);

        client.ShouldNotBeNull();
    }

    [Fact]
    public void CreateEmbeddingGenerator_WithValidOptions_ReturnsGenerator()
    {
        OpenAIProviderFactory factory = CreateFactory();
        AIWorkspace workspace = CreateWorkspace();

        IEmbeddingGenerator<string, Embedding<float>>? generator = factory.CreateEmbeddingGenerator(workspace);

        generator.ShouldNotBeNull();
    }
}
