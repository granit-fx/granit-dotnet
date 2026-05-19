using Granit.AI.OpenAI.Internal;
using Granit.AI.OpenAI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Shouldly;

namespace Granit.AI.OpenAI.Tests;

public sealed class OpenAIProviderFactoryTests
{
    private static OpenAIProviderFactory CreateFactory(
        out TestOptionsMonitor<OpenAIProviderOptions> monitor,
        OpenAIProviderOptions? options = null)
    {
        monitor = new TestOptionsMonitor<OpenAIProviderOptions>(options ?? TestFixtures.DefaultOptions());
        return new OpenAIProviderFactory(monitor, new TestHttpClientFactory(), TimeProvider.System);
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
        OpenAIProviderFactory factory = CreateFactory(out _);

        factory.ProviderName.ShouldBe("OpenAI");
    }

    [Fact]
    public void CreateChatClient_WithValidOptions_ReturnsClient()
    {
        OpenAIProviderFactory factory = CreateFactory(out _);

        IChatClient client = factory.CreateChatClient(CreateWorkspace());

        client.ShouldNotBeNull();
    }

    [Fact]
    public void CreateEmbeddingGenerator_WithValidOptions_ReturnsGenerator()
    {
        OpenAIProviderFactory factory = CreateFactory(out _);

        IEmbeddingGenerator<string, Embedding<float>>? generator = factory.CreateEmbeddingGenerator(CreateWorkspace());

        generator.ShouldNotBeNull();
    }

    [Fact]
    public void CreateChatClient_WithNullWorkspace_Throws()
    {
        OpenAIProviderFactory factory = CreateFactory(out _);

        Should.Throw<ArgumentNullException>(() => factory.CreateChatClient(null!));
    }

    [Fact]
    public void CreateEmbeddingGenerator_WithNullWorkspace_Throws()
    {
        OpenAIProviderFactory factory = CreateFactory(out _);

        Should.Throw<ArgumentNullException>(() => factory.CreateEmbeddingGenerator(null!));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        OpenAIProviderFactory factory = CreateFactory(out _);

        factory.Dispose();

        Should.NotThrow(() => factory.Dispose());
    }

    [Fact]
    public void CreateChatClient_WithModelOutsideAllowlist_Throws()
    {
        OpenAIProviderOptions options = TestFixtures.DefaultOptions();
        options.AllowedModels = ["gpt-4o", "text-embedding-3-small"];
        OpenAIProviderFactory factory = CreateFactory(out _, options);

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => factory.CreateChatClient(CreateWorkspace("o3")));

        ex.Message.ShouldContain("AllowedModels");
        ex.Message.ShouldContain("o3");
    }

    [Fact]
    public void CreateEmbeddingGenerator_WithEmbeddingModelOutsideAllowlist_Throws()
    {
        OpenAIProviderOptions options = TestFixtures.DefaultOptions();
        // Mismatched allowlist: chat model in, embedding model NOT in.
        options.AllowedModels = ["gpt-4o"];
        // The validator would normally reject this configuration, but the factory enforces it at call time too.
        OpenAIProviderFactory factory = CreateFactory(out _, options);

        Should.Throw<InvalidOperationException>(
            () => factory.CreateEmbeddingGenerator(CreateWorkspace()));
    }

    [Fact]
    public void CreateChatClient_FallsBackToDefaultModel_WhenWorkspaceModelBlank()
    {
        OpenAIProviderOptions options = TestFixtures.DefaultOptions();
        options.AllowedModels = ["gpt-4o", "text-embedding-3-small"];
        OpenAIProviderFactory factory = CreateFactory(out _, options);

        IChatClient client = factory.CreateChatClient(CreateWorkspace(""));

        client.ShouldNotBeNull();
    }

    [Fact]
    public void OptionsChange_TightensAllowlist_NewCallsAreRejected()
    {
        OpenAIProviderFactory factory = CreateFactory(out TestOptionsMonitor<OpenAIProviderOptions> monitor);

        factory.CreateChatClient(CreateWorkspace("o3")).ShouldNotBeNull();

        OpenAIProviderOptions tightened = TestFixtures.DefaultOptions();
        tightened.AllowedModels = ["gpt-4o", "text-embedding-3-small"];
        monitor.Set(tightened);

        Should.Throw<InvalidOperationException>(
            () => factory.CreateChatClient(CreateWorkspace("o3")));
    }

    [Fact]
    public void OptionsChange_RotatesApiKey_FactoryStillServesRequests()
    {
        OpenAIProviderFactory factory = CreateFactory(out TestOptionsMonitor<OpenAIProviderOptions> monitor);

        IChatClient before = factory.CreateChatClient(CreateWorkspace());

        OpenAIProviderOptions rotated = TestFixtures.DefaultOptions();
        rotated.ApiKey = "sk-rotated-key";
        monitor.Set(rotated);

        IChatClient after = factory.CreateChatClient(CreateWorkspace());

        before.ShouldNotBeNull();
        after.ShouldNotBeNull();
    }
}
