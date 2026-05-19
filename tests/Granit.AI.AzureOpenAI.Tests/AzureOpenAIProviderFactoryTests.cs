using Granit.AI.AzureOpenAI.Internal;
using Granit.AI.AzureOpenAI.Options;
using Granit.AI.Tenancy;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Shouldly;

namespace Granit.AI.AzureOpenAI.Tests;

public sealed class AzureOpenAIProviderFactoryTests
{
    private static AIWorkspace CreateWorkspace(string? model = "gpt-4o", string? apiKey = null, string? endpoint = null) =>
        new()
        {
            Name = "test",
            Provider = "AzureOpenAI",
            Model = model!,
            ApiKey = apiKey,
            Endpoint = endpoint,
        };

    [Fact]
    public void ProviderName_IsAzureOpenAI()
    {
        (AzureOpenAIProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        factory.ProviderName.ShouldBe("AzureOpenAI");
    }

    [Fact]
    public async Task CreateChatClientAsync_WithHostFallback_ReturnsClient()
    {
        (AzureOpenAIProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        IChatClient client = await factory.CreateChatClientAsync(CreateWorkspace(), TestContext.Current.CancellationToken);

        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateChatClientAsync_NoApiKey_NoManagedIdentityFallback_Throws()
    {
        AzureOpenAIProviderOptions options = TestFixtures.DefaultOptions();
        options.ApiKey = null;
        // AllowManagedIdentityFallback defaults to false
        (AzureOpenAIProviderFactory factory, _, _, _) = TestFixtures.BuildFactory(options);

        await Should.ThrowAsync<AIProviderCredentialNotConfiguredException>(
            async () => await factory.CreateChatClientAsync(CreateWorkspace(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateChatClientAsync_NoApiKey_WithManagedIdentityFallback_ReturnsClient()
    {
        AzureOpenAIProviderOptions options = TestFixtures.DefaultOptions();
        options.ApiKey = null;
        options.AllowManagedIdentityFallback = true;
        (AzureOpenAIProviderFactory factory, _, _, _) = TestFixtures.BuildFactory(options);

        IChatClient client = await factory.CreateChatClientAsync(CreateWorkspace(), TestContext.Current.CancellationToken);

        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateChatClientAsync_DeploymentOutsideAllowlist_Throws()
    {
        AzureOpenAIProviderOptions options = TestFixtures.DefaultOptions();
        options.AllowedDeployments = ["gpt-4o", "text-embedding-3-small"];
        (AzureOpenAIProviderFactory factory, _, _, _) = TestFixtures.BuildFactory(options);

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await factory.CreateChatClientAsync(CreateWorkspace("o3-mini"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateChatClientAsync_TenantSetting_OverridesHostOptions()
    {
        (AzureOpenAIProviderFactory factory, TestSettingValueProvider tenant, _, _) = TestFixtures.BuildFactory();

        tenant.Set(AISettingNames.AzureOpenAI.ApiKey, "tenant-key");
        tenant.Set(AISettingNames.AzureOpenAI.Endpoint, "https://tenant-resource.openai.azure.com");

        IChatClient client = await factory.CreateChatClientAsync(
            CreateWorkspace() with { TenantId = Guid.NewGuid() },
            TestContext.Current.CancellationToken);

        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateChatClientAsync_NullWorkspace_Throws()
    {
        (AzureOpenAIProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        await Should.ThrowAsync<ArgumentNullException>(
            async () => await factory.CreateChatClientAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAvailableModelsAsync_ReturnsConfiguredDeployments()
    {
        (AzureOpenAIProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        IReadOnlyList<AIModelInfo> models = await factory.GetAvailableModelsAsync(TestContext.Current.CancellationToken);

        models.Count.ShouldBe(2);
    }
}
