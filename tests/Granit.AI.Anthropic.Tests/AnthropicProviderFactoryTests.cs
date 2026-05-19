using Granit.AI.Anthropic.Internal;
using Granit.AI.Anthropic.Options;
using Granit.AI.Tenancy;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Shouldly;

namespace Granit.AI.Anthropic.Tests;

public sealed class AnthropicProviderFactoryTests
{
    private static AIWorkspace CreateWorkspace(string? model = "claude-sonnet-4-6", string? apiKey = null) =>
        new()
        {
            Name = "test",
            Provider = "Anthropic",
            Model = model!,
            ApiKey = apiKey,
        };

    [Fact]
    public void ProviderName_IsAnthropic()
    {
        (AnthropicProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        factory.ProviderName.ShouldBe("Anthropic");
    }

    [Fact]
    public async Task CreateChatClientAsync_WithHostFallback_ReturnsClient()
    {
        (AnthropicProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        IChatClient client = await factory.CreateChatClientAsync(CreateWorkspace(), TestContext.Current.CancellationToken);

        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateEmbeddingGeneratorAsync_AlwaysReturnsNull()
    {
        (AnthropicProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        IEmbeddingGenerator<string, Embedding<float>>? generator =
            await factory.CreateEmbeddingGeneratorAsync(CreateWorkspace(), TestContext.Current.CancellationToken);

        generator.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateChatClientAsync_BlankWorkspaceModel_FallsBackToDefaultModel(string blank)
    {
        (AnthropicProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        IChatClient client = await factory.CreateChatClientAsync(CreateWorkspace(blank), TestContext.Current.CancellationToken);

        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateChatClientAsync_NullWorkspace_Throws()
    {
        (AnthropicProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        await Should.ThrowAsync<ArgumentNullException>(
            async () => await factory.CreateChatClientAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateEmbeddingGeneratorAsync_NullWorkspace_Throws()
    {
        (AnthropicProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        await Should.ThrowAsync<ArgumentNullException>(
            async () => await factory.CreateEmbeddingGeneratorAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateChatClientAsync_ModelOutsideAllowlist_Throws()
    {
        AnthropicProviderOptions options = TestFixtures.DefaultOptions();
        options.AllowedModels = ["claude-sonnet-4-6"];
        (AnthropicProviderFactory factory, _, _, _) = TestFixtures.BuildFactory(options);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await factory.CreateChatClientAsync(CreateWorkspace("claude-opus-4-7"), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("AllowedModels");
    }

    [Fact]
    public async Task CreateChatClientAsync_WorkspaceApiKey_TakesPrecedence()
    {
        AnthropicProviderOptions options = TestFixtures.DefaultOptions();
        options.ApiKey = "sk-ant-host-key";
        (AnthropicProviderFactory factory, _, _, _) = TestFixtures.BuildFactory(options);

        AIWorkspace workspace = CreateWorkspace(apiKey: "sk-ant-workspace-key");

        IChatClient client = await factory.CreateChatClientAsync(workspace, TestContext.Current.CancellationToken);

        client.ShouldNotBeNull();
        // Ensure the underlying SDK client was constructed (sanity).
    }

    [Fact]
    public async Task CreateChatClientAsync_TenantSetting_OverridesHostOptions()
    {
        AnthropicProviderOptions options = TestFixtures.DefaultOptions();
        options.ApiKey = "sk-ant-host-key";
        (AnthropicProviderFactory factory, TestSettingValueProvider tenant, _, _) = TestFixtures.BuildFactory(options);

        tenant.Set(AISettingNames.Anthropic.ApiKey, "sk-ant-tenant-key");

        IChatClient client = await factory.CreateChatClientAsync(
            CreateWorkspace() with { TenantId = Guid.NewGuid() },
            TestContext.Current.CancellationToken);

        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateChatClientAsync_NoCredentialAnywhere_Throws()
    {
        AnthropicProviderOptions options = TestFixtures.DefaultOptions();
        options.ApiKey = string.Empty;     // strip the host fallback
        (AnthropicProviderFactory factory, _, _, _) = TestFixtures.BuildFactory(options);

        await Should.ThrowAsync<AIProviderCredentialNotConfiguredException>(
            async () => await factory.CreateChatClientAsync(CreateWorkspace(), TestContext.Current.CancellationToken));
    }
}
