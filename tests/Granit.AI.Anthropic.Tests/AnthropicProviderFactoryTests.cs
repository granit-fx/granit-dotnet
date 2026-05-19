using Granit.AI.Anthropic.Internal;
using Granit.AI.Anthropic.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Shouldly;

namespace Granit.AI.Anthropic.Tests;

public sealed class AnthropicProviderFactoryTests
{
    private static AIWorkspace CreateWorkspace(string? model = "claude-sonnet-4-6") =>
        new()
        {
            Name = "test",
            Provider = "Anthropic",
            Model = model!,
        };

    private static AnthropicProviderFactory CreateFactory(
        out TestOptionsMonitor<AnthropicProviderOptions> monitor,
        AnthropicProviderOptions? options = null)
    {
        monitor = new TestOptionsMonitor<AnthropicProviderOptions>(options ?? TestFixtures.DefaultOptions());
        return new AnthropicProviderFactory(monitor, new TestHttpClientFactory());
    }

    [Fact]
    public void ProviderName_IsAnthropic()
    {
        AnthropicProviderFactory factory = CreateFactory(out _);

        factory.ProviderName.ShouldBe("Anthropic");
    }

    [Fact]
    public void CreateChatClient_WithValidOptions_ReturnsClient()
    {
        AnthropicProviderFactory factory = CreateFactory(out _);

        IChatClient client = factory.CreateChatClient(CreateWorkspace());

        client.ShouldNotBeNull();
    }

    [Fact]
    public void CreateEmbeddingGenerator_ReturnsNull()
    {
        AnthropicProviderFactory factory = CreateFactory(out _);

        IEmbeddingGenerator<string, Embedding<float>>? generator = factory.CreateEmbeddingGenerator(CreateWorkspace());

        generator.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateChatClient_WithBlankWorkspaceModel_FallsBackToDefaultModel(string blankModel)
    {
        AnthropicProviderFactory factory = CreateFactory(out _);

        IChatClient client = factory.CreateChatClient(CreateWorkspace(blankModel));

        client.ShouldNotBeNull();
    }

    [Fact]
    public void CreateChatClient_WithNullWorkspace_Throws()
    {
        AnthropicProviderFactory factory = CreateFactory(out _);

        Should.Throw<ArgumentNullException>(() => factory.CreateChatClient(null!));
    }

    [Fact]
    public void CreateEmbeddingGenerator_WithNullWorkspace_Throws()
    {
        AnthropicProviderFactory factory = CreateFactory(out _);

        Should.Throw<ArgumentNullException>(() => factory.CreateEmbeddingGenerator(null!));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        AnthropicProviderFactory factory = CreateFactory(out _);

        factory.Dispose();

        Should.NotThrow(() => factory.Dispose());
    }

    [Fact]
    public void CreateChatClient_WithModelOutsideAllowlist_Throws()
    {
        AnthropicProviderOptions options = TestFixtures.DefaultOptions();
        options.AllowedModels = ["claude-sonnet-4-6"];
        AnthropicProviderFactory factory = CreateFactory(out _, options);

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => factory.CreateChatClient(CreateWorkspace("claude-opus-4-7")));

        ex.Message.ShouldContain("AllowedModels");
        ex.Message.ShouldContain("claude-opus-4-7");
    }

    [Fact]
    public void CreateChatClient_WithModelInsideAllowlist_Succeeds()
    {
        AnthropicProviderOptions options = TestFixtures.DefaultOptions();
        options.AllowedModels = ["claude-sonnet-4-6", "claude-haiku-4-5"];
        AnthropicProviderFactory factory = CreateFactory(out _, options);

        IChatClient client = factory.CreateChatClient(CreateWorkspace("claude-haiku-4-5"));

        client.ShouldNotBeNull();
    }

    [Fact]
    public void CreateChatClient_WithEmptyAllowlist_AllowsAnyModel()
    {
        AnthropicProviderFactory factory = CreateFactory(out _);

        IChatClient client = factory.CreateChatClient(CreateWorkspace("claude-opus-4-7"));

        client.ShouldNotBeNull();
    }

    [Fact]
    public void OptionsChange_TightensAllowlist_NewCallsAreRejected()
    {
        AnthropicProviderFactory factory = CreateFactory(out TestOptionsMonitor<AnthropicProviderOptions> monitor);

        // Before rotation: any model is accepted.
        factory.CreateChatClient(CreateWorkspace("claude-opus-4-7")).ShouldNotBeNull();

        AnthropicProviderOptions tightened = TestFixtures.DefaultOptions();
        tightened.AllowedModels = ["claude-sonnet-4-6"];
        monitor.Set(tightened);

        Should.Throw<InvalidOperationException>(
            () => factory.CreateChatClient(CreateWorkspace("claude-opus-4-7")));
    }

    [Fact]
    public void OptionsChange_RotatesApiKey_FactoryStillServesRequests()
    {
        AnthropicProviderFactory factory = CreateFactory(out TestOptionsMonitor<AnthropicProviderOptions> monitor);

        IChatClient before = factory.CreateChatClient(CreateWorkspace());

        AnthropicProviderOptions rotated = TestFixtures.DefaultOptions();
        rotated.ApiKey = "sk-ant-rotated-key";
        monitor.Set(rotated);

        IChatClient after = factory.CreateChatClient(CreateWorkspace());

        before.ShouldNotBeNull();
        after.ShouldNotBeNull();
    }
}
