using Granit.AI.Anthropic.Internal;
using Granit.AI.Anthropic.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Granit.AI.Anthropic.Tests;

public sealed class AnthropicProviderFactoryTests
{
    private static readonly IOptions<AnthropicProviderOptions> DefaultOptions =
        Microsoft.Extensions.Options.Options.Create(new AnthropicProviderOptions
        {
            ApiKey = "sk-ant-test-key",
            DefaultModel = "claude-sonnet-4-6",
        });

    private static AIWorkspace CreateWorkspace(string? model = "claude-sonnet-4-6") =>
        new()
        {
            Name = "test",
            Provider = "Anthropic",
            Model = model!,
        };

    [Fact]
    public void ProviderName_IsAnthropic()
    {
        var factory = new AnthropicProviderFactory(DefaultOptions);

        factory.ProviderName.ShouldBe("Anthropic");
    }

    [Fact]
    public void CreateChatClient_WithValidOptions_ReturnsClient()
    {
        var factory = new AnthropicProviderFactory(DefaultOptions);
        AIWorkspace workspace = CreateWorkspace();

        IChatClient client = factory.CreateChatClient(workspace);

        client.ShouldNotBeNull();
    }

    [Fact]
    public void CreateEmbeddingGenerator_ReturnsNull()
    {
        var factory = new AnthropicProviderFactory(DefaultOptions);
        AIWorkspace workspace = CreateWorkspace();

        IEmbeddingGenerator<string, Embedding<float>>? generator = factory.CreateEmbeddingGenerator(workspace);

        generator.ShouldBeNull();
    }
}
