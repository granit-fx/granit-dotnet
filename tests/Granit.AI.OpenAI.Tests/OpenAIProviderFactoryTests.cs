using Granit.AI.Exceptions;
using Granit.AI.OpenAI.Internal;
using Granit.AI.OpenAI.Options;
using Granit.AI.Tenancy;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Shouldly;

namespace Granit.AI.OpenAI.Tests;

public sealed class OpenAIProviderFactoryTests
{
    private static AIWorkspace CreateWorkspace(string model = "gpt-4o", string? apiKey = null, string? endpoint = null) =>
        new()
        {
            Name = "test-workspace",
            Provider = "OpenAI",
            Model = model,
            ApiKey = apiKey,
            Endpoint = endpoint,
        };

    [Fact]
    public void ProviderName_IsOpenAI()
    {
        (OpenAIProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        factory.ProviderName.ShouldBe("OpenAI");
    }

    [Fact]
    public async Task CreateChatClientAsync_WithHostFallback_ReturnsClient()
    {
        (OpenAIProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        IChatClient client = await factory.CreateChatClientAsync(CreateWorkspace(), TestContext.Current.CancellationToken);

        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateEmbeddingGeneratorAsync_WithHostFallback_ReturnsGenerator()
    {
        (OpenAIProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        IEmbeddingGenerator<string, Embedding<float>>? generator =
            await factory.CreateEmbeddingGeneratorAsync(CreateWorkspace(), TestContext.Current.CancellationToken);

        generator.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateChatClientAsync_NullWorkspace_Throws()
    {
        (OpenAIProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        await Should.ThrowAsync<ArgumentNullException>(
            async () => await factory.CreateChatClientAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateChatClientAsync_ModelOutsideAllowlist_Throws()
    {
        OpenAIProviderOptions options = TestFixtures.DefaultOptions();
        options.AllowedModels = ["gpt-4o", "text-embedding-3-small"];
        (OpenAIProviderFactory factory, _, _, _) = TestFixtures.BuildFactory(options);

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await factory.CreateChatClientAsync(CreateWorkspace("o3"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateChatClientAsync_WorkspaceApiKey_TakesPrecedence()
    {
        OpenAIProviderOptions options = TestFixtures.DefaultOptions();
        options.ApiKey = "sk-host-key";
        (OpenAIProviderFactory factory, _, _, _) = TestFixtures.BuildFactory(options);

        IChatClient client = await factory.CreateChatClientAsync(
            CreateWorkspace(apiKey: "sk-workspace-key"),
            TestContext.Current.CancellationToken);

        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateChatClientAsync_TenantSetting_OverridesHostOptions()
    {
        (OpenAIProviderFactory factory, TestSettingValueProvider tenant, _, _) = TestFixtures.BuildFactory();
        tenant.Set(AISettingNames.OpenAI.ApiKey, "sk-tenant-key");

        IChatClient client = await factory.CreateChatClientAsync(
            CreateWorkspace() with { TenantId = Guid.NewGuid() },
            TestContext.Current.CancellationToken);

        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateChatClientAsync_InvalidEndpoint_Throws()
    {
        (OpenAIProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        // 169.254.169.254 is a hard-blocked metadata IP regardless of policy. ApiKey supplied to
        // ensure the cascade reaches the endpoint validation step (NotConfigured otherwise).
        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await factory.CreateChatClientAsync(
                CreateWorkspace(apiKey: "sk-test-w", endpoint: "https://169.254.169.254/v1/"),
                TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("endpoint");
    }

    [Fact]
    public async Task CreateChatClientAsync_NoCredentialAnywhere_Throws()
    {
        OpenAIProviderOptions options = TestFixtures.DefaultOptions();
        options.ApiKey = string.Empty;
        (OpenAIProviderFactory factory, _, _, _) = TestFixtures.BuildFactory(options);

        await Should.ThrowAsync<AIProviderCredentialNotConfiguredException>(
            async () => await factory.CreateChatClientAsync(CreateWorkspace(), TestContext.Current.CancellationToken));
    }
}
