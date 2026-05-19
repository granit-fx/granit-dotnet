using Granit.AI.AzureOpenAI.Internal;
using Granit.AI.AzureOpenAI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Shouldly;

namespace Granit.AI.AzureOpenAI.Tests;

public sealed class AzureOpenAIProviderFactoryTests
{
    private static AzureOpenAIProviderFactory CreateFactory(
        out TestOptionsMonitor<AzureOpenAIProviderOptions> monitor,
        AzureOpenAIProviderOptions? options = null)
    {
        monitor = new TestOptionsMonitor<AzureOpenAIProviderOptions>(options ?? TestFixtures.DefaultOptions());
        return new AzureOpenAIProviderFactory(monitor, new TestHttpClientFactory());
    }

    private static AIWorkspace CreateWorkspace(string? model = "gpt-4o") =>
        new()
        {
            Name = "test",
            Provider = "AzureOpenAI",
            Model = model!,
        };

    [Fact]
    public void ProviderName_IsAzureOpenAI()
    {
        AzureOpenAIProviderFactory factory = CreateFactory(out _);

        factory.ProviderName.ShouldBe("AzureOpenAI");
    }

    [Fact]
    public void CreateChatClient_WithApiKey_ReturnsClient()
    {
        AzureOpenAIProviderFactory factory = CreateFactory(out _);

        IChatClient client = factory.CreateChatClient(CreateWorkspace());

        client.ShouldNotBeNull();
    }

    [Fact]
    public void CreateChatClient_WithManagedIdentity_ReturnsClient()
    {
        AzureOpenAIProviderOptions options = TestFixtures.DefaultOptions();
        options.ApiKey = null;
        AzureOpenAIProviderFactory factory = CreateFactory(out _, options);

        IChatClient client = factory.CreateChatClient(CreateWorkspace());

        client.ShouldNotBeNull();
    }

    [Fact]
    public void CreateEmbeddingGenerator_ReturnsGenerator()
    {
        AzureOpenAIProviderFactory factory = CreateFactory(out _);

        IEmbeddingGenerator<string, Embedding<float>>? generator =
            factory.CreateEmbeddingGenerator(CreateWorkspace());

        generator.ShouldNotBeNull();
    }

    [Fact]
    public void CreateChatClient_WithNullWorkspace_Throws()
    {
        AzureOpenAIProviderFactory factory = CreateFactory(out _);

        Should.Throw<ArgumentNullException>(() => factory.CreateChatClient(null!));
    }

    [Fact]
    public void CreateEmbeddingGenerator_WithNullWorkspace_Throws()
    {
        AzureOpenAIProviderFactory factory = CreateFactory(out _);

        Should.Throw<ArgumentNullException>(() => factory.CreateEmbeddingGenerator(null!));
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        AzureOpenAIProviderFactory factory = CreateFactory(out _);

        factory.Dispose();

        Should.NotThrow(() => factory.Dispose());
    }

    [Fact]
    public void CreateChatClient_WithDeploymentOutsideAllowlist_Throws()
    {
        AzureOpenAIProviderOptions options = TestFixtures.DefaultOptions();
        options.AllowedDeployments = ["gpt-4o", "text-embedding-3-small"];
        AzureOpenAIProviderFactory factory = CreateFactory(out _, options);

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => factory.CreateChatClient(CreateWorkspace("o3-mini")));

        ex.Message.ShouldContain("AllowedDeployments");
        ex.Message.ShouldContain("o3-mini");
    }

    [Fact]
    public void CreateChatClient_FallsBackToDefaultDeployment_WhenWorkspaceModelBlank()
    {
        AzureOpenAIProviderOptions options = TestFixtures.DefaultOptions();
        options.AllowedDeployments = ["gpt-4o", "text-embedding-3-small"];
        AzureOpenAIProviderFactory factory = CreateFactory(out _, options);

        IChatClient client = factory.CreateChatClient(CreateWorkspace(""));

        client.ShouldNotBeNull();
    }

    [Fact]
    public void OptionsChange_TightensAllowlist_NewCallsAreRejected()
    {
        AzureOpenAIProviderFactory factory = CreateFactory(out TestOptionsMonitor<AzureOpenAIProviderOptions> monitor);

        factory.CreateChatClient(CreateWorkspace("o3-mini")).ShouldNotBeNull();

        AzureOpenAIProviderOptions tightened = TestFixtures.DefaultOptions();
        tightened.AllowedDeployments = ["gpt-4o", "text-embedding-3-small"];
        monitor.Set(tightened);

        Should.Throw<InvalidOperationException>(
            () => factory.CreateChatClient(CreateWorkspace("o3-mini")));
    }

    [Fact]
    public void OptionsChange_RotatesApiKey_FactoryStillServesRequests()
    {
        AzureOpenAIProviderFactory factory = CreateFactory(out TestOptionsMonitor<AzureOpenAIProviderOptions> monitor);

        IChatClient before = factory.CreateChatClient(CreateWorkspace());

        AzureOpenAIProviderOptions rotated = TestFixtures.DefaultOptions();
        rotated.ApiKey = "rotated-key";
        monitor.Set(rotated);

        IChatClient after = factory.CreateChatClient(CreateWorkspace());

        before.ShouldNotBeNull();
        after.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAvailableModelsAsync_ReturnsConfiguredDeployments()
    {
        AzureOpenAIProviderFactory factory = CreateFactory(out _);

        IReadOnlyList<AIModelInfo> models = await factory.GetAvailableModelsAsync(TestContext.Current.CancellationToken);

        models.Count.ShouldBe(2);
        models.ShouldContain(m => m.Id == "gpt-4o");
        models.ShouldContain(m => m.Id == "text-embedding-3-small");
    }
}
