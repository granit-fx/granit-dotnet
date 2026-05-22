using Granit.AI.Exceptions;
using Granit.AI.Ollama.Internal;
using Granit.AI.Ollama.Options;
using Granit.AI.Tenancy;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using OllamaSharp;
using Shouldly;

namespace Granit.AI.Ollama.Tests;

public sealed class OllamaProviderFactoryTests
{
    private static AIWorkspace CreateWorkspace(string? model = "llama3.2", string? endpoint = null) =>
        new()
        {
            Name = "test",
            Provider = "Ollama",
            Model = model!,
            Endpoint = endpoint,
        };

    [Fact]
    public void ProviderName_IsOllama()
    {
        (OllamaProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        factory.ProviderName.ShouldBe("Ollama");
    }

    [Fact]
    public async Task CreateChatClientAsync_WithHostFallback_ReturnsClient()
    {
        (OllamaProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        IChatClient client = await factory.CreateChatClientAsync(CreateWorkspace(), TestContext.Current.CancellationToken);

        client.ShouldNotBeNull();
        client.GetService(typeof(OllamaApiClient)).ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateEmbeddingGeneratorAsync_ReturnsClient()
    {
        (OllamaProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        IEmbeddingGenerator<string, Embedding<float>>? generator =
            await factory.CreateEmbeddingGeneratorAsync(CreateWorkspace("nomic-embed-text"), TestContext.Current.CancellationToken);

        generator.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateChatClientAsync_NullWorkspace_Throws()
    {
        (OllamaProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        await Should.ThrowAsync<ArgumentNullException>(
            async () => await factory.CreateChatClientAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateChatClientAsync_TenantEndpointSetting_OverridesHostOptions()
    {
        (OllamaProviderFactory factory, TestSettingValueProvider tenant, _, _) = TestFixtures.BuildFactory();
        tenant.Set(AISettingNames.Ollama.Endpoint, "http://localhost:11434");

        IChatClient client = await factory.CreateChatClientAsync(
            CreateWorkspace() with { TenantId = Guid.NewGuid() },
            TestContext.Current.CancellationToken);

        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateChatClientAsync_WorkspaceEndpoint_TakesPrecedence()
    {
        (OllamaProviderFactory factory, _, _, _) = TestFixtures.BuildFactory();

        IChatClient client = await factory.CreateChatClientAsync(
            CreateWorkspace(endpoint: "http://127.0.0.1"),
            TestContext.Current.CancellationToken);

        client.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateChatClientAsync_TenantEndpointToPrivateIp_Throws()
    {
        // Tenant policy disallows private (RFC 1918) IPs even though loopback is allowed.
        (OllamaProviderFactory factory, TestSettingValueProvider tenant, _, _) = TestFixtures.BuildFactory();
        tenant.Set(AISettingNames.Ollama.Endpoint, "http://10.0.0.5:11434");

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await factory.CreateChatClientAsync(
                CreateWorkspace() with { TenantId = Guid.NewGuid() },
                TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("endpoint");
    }

    [Fact]
    public async Task CreateChatClientAsync_ModelOutsideAllowlist_Throws()
    {
        OllamaProviderOptions options = TestFixtures.DefaultOptions();
        options.AllowedModels = ["llama3.2"];
        (OllamaProviderFactory factory, _, _, _) = TestFixtures.BuildFactory(options);

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await factory.CreateChatClientAsync(CreateWorkspace("phi3"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateChatClientAsync_NoEndpointAnywhere_Throws()
    {
        OllamaProviderOptions options = TestFixtures.DefaultOptions();
        options.Endpoint = string.Empty;
        (OllamaProviderFactory factory, _, _, _) = TestFixtures.BuildFactory(options);

        await Should.ThrowAsync<AIProviderCredentialNotConfiguredException>(
            async () => await factory.CreateChatClientAsync(CreateWorkspace(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateChatClientAsync_HostEndpointToPrivateIp_RoutesThroughHostHttpClient()
    {
        // The HostPermissive policy allows operator-trusted private deployments. Before the
        // fix, a single shared HttpClient was wired to the strict tenant connect callback —
        // so even a host-options private IP was rejected at TCP connect. The cache now selects
        // the Host-named client (which carries HostPermissive) when credential.Scope == Host.
        OllamaProviderOptions options = new()
        {
            Endpoint = "http://192.168.10.13:11434",
            DefaultModel = "llama3.2",
        };
        (OllamaProviderFactory factory, _, _, _, TestHttpClientFactory http) =
            TestFixtures.BuildFactoryWithHttp(options);

        IChatClient client = await factory.CreateChatClientAsync(
            CreateWorkspace(), TestContext.Current.CancellationToken);

        client.ShouldNotBeNull();
        http.RequestedNames.ShouldContain(OllamaProviderFactory.HostHttpClientName);
        http.RequestedNames.ShouldNotContain(OllamaProviderFactory.TenantHttpClientName);
    }

    [Fact]
    public async Task CreateChatClientAsync_WorkspaceEndpoint_RoutesThroughTenantHttpClient()
    {
        // Workspace-scoped endpoint must keep the strict OllamaTenant connect policy so DNS
        // rebinding to a private IP at call time still gets blocked.
        (OllamaProviderFactory factory, _, _, _, TestHttpClientFactory http) =
            TestFixtures.BuildFactoryWithHttp();

        IChatClient client = await factory.CreateChatClientAsync(
            CreateWorkspace(endpoint: "http://127.0.0.1:11434"),
            TestContext.Current.CancellationToken);

        client.ShouldNotBeNull();
        http.RequestedNames.ShouldContain(OllamaProviderFactory.TenantHttpClientName);
        http.RequestedNames.ShouldNotContain(OllamaProviderFactory.HostHttpClientName);
    }
}
