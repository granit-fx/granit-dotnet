using Granit.AI.Internal;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Granit.AI.Tests;

public sealed class DefaultAIWorkspaceCapabilityResolverTests
{
    [Fact]
    public async Task ResolveAsync_UnknownProvider_ReturnsNull()
    {
        DefaultAIWorkspaceCapabilityResolver resolver = new(
            [],
            NullLogger<DefaultAIWorkspaceCapabilityResolver>.Instance);

        AIModelCapabilities? capabilities = await resolver
            .ResolveAsync("Nonexistent", "model", TestContext.Current.CancellationToken);

        capabilities.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_ProviderWithoutCatalog_ReturnsNull()
    {
        DefaultAIWorkspaceCapabilityResolver resolver = new(
            [new StubFactory()],
            NullLogger<DefaultAIWorkspaceCapabilityResolver>.Instance);

        AIModelCapabilities? capabilities = await resolver
            .ResolveAsync("Stub", "model", TestContext.Current.CancellationToken);

        capabilities.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_CatalogReturnsModel_ReturnsCapabilities()
    {
        AIModelCapabilities expected = new() { Chat = true, Vision = true };
        StubCatalogFactory factory = new(
            "Provider",
            models: [new AIModelInfo("llama3.2", "llama3.2", expected)]);
        DefaultAIWorkspaceCapabilityResolver resolver = new(
            [factory],
            NullLogger<DefaultAIWorkspaceCapabilityResolver>.Instance);

        AIModelCapabilities? capabilities = await resolver
            .ResolveAsync("Provider", "llama3.2", TestContext.Current.CancellationToken);

        capabilities.ShouldBe(expected);
    }

    [Fact]
    public async Task ResolveAsync_CatalogThrowsHttpRequestException_ReturnsNullInsteadOfPropagating()
    {
        // Repro of the page-crash: Ollama unreachable bubbled an HttpRequestException up to
        // AIWorkspaceEndpoints.ListAllAsync. The resolver now degrades to "unknown".
        StubCatalogFactory factory = new(
            "Provider",
            throwOnGetModels: new HttpRequestException("connect refused"));
        DefaultAIWorkspaceCapabilityResolver resolver = new(
            [factory],
            NullLogger<DefaultAIWorkspaceCapabilityResolver>.Instance);

        AIModelCapabilities? capabilities = await resolver
            .ResolveAsync("Provider", "llama3.2", TestContext.Current.CancellationToken);

        capabilities.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_CatalogThrowsInvalidOperation_ReturnsNullInsteadOfPropagating()
    {
        // Endpoint validation failure (e.g. tenant endpoint with a private IP) throws
        // InvalidOperationException at credential resolution; must not crash the listing.
        StubCatalogFactory factory = new(
            "Provider",
            throwOnGetModels: new InvalidOperationException("endpoint validation failed"));
        DefaultAIWorkspaceCapabilityResolver resolver = new(
            [factory],
            NullLogger<DefaultAIWorkspaceCapabilityResolver>.Instance);

        AIModelCapabilities? capabilities = await resolver
            .ResolveAsync("Provider", "llama3.2", TestContext.Current.CancellationToken);

        capabilities.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_CallerCancellation_Propagates()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        StubCatalogFactory factory = new(
            "Provider",
            throwOnGetModels: new OperationCanceledException(cts.Token));
        DefaultAIWorkspaceCapabilityResolver resolver = new(
            [factory],
            NullLogger<DefaultAIWorkspaceCapabilityResolver>.Instance);

        await Should.ThrowAsync<OperationCanceledException>(
            async () => await resolver.ResolveAsync("Provider", "model", cts.Token));
    }

    private sealed class StubFactory : IAIProviderFactory
    {
        public string ProviderName => "Stub";

        public ValueTask<IChatClient> CreateChatClientAsync(AIWorkspace workspace, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public ValueTask<IEmbeddingGenerator<string, Embedding<float>>?> CreateEmbeddingGeneratorAsync(
            AIWorkspace workspace, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class StubCatalogFactory(
        string providerName,
        IReadOnlyList<AIModelInfo>? models = null,
        Exception? throwOnGetModels = null) : IAIProviderFactory, IAIModelCatalog
    {
        public string ProviderName => providerName;

        public ValueTask<IChatClient> CreateChatClientAsync(AIWorkspace workspace, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public ValueTask<IEmbeddingGenerator<string, Embedding<float>>?> CreateEmbeddingGeneratorAsync(
            AIWorkspace workspace, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<AIModelInfo>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        {
            if (throwOnGetModels is not null)
            {
                throw throwOnGetModels;
            }
            return Task.FromResult(models ?? (IReadOnlyList<AIModelInfo>)[]);
        }
    }
}
