using Granit.AI.Exceptions;
using Granit.AI.Internal;
using Granit.AI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Tests;

public sealed class DefaultAIEmbeddingGeneratorFactoryTests
{
    private readonly IAIWorkspaceProvider _workspaceProvider = Substitute.For<IAIWorkspaceProvider>();
    private readonly IOptions<GranitAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new GranitAIOptions { DefaultWorkspace = "default" });

    private static AIWorkspace CreateWorkspace(string name = "test", string provider = "OpenAI") =>
        new() { Name = name, Provider = provider, Model = "text-embedding-3-small" };

    [Fact]
    public async Task CreateAsync_KnownWorkspaceAndProvider_ReturnsEmbeddingGenerator()
    {
        AIWorkspace workspace = CreateWorkspace();
        _workspaceProvider.GetAsync("test", Arg.Any<CancellationToken>()).Returns(workspace);

        IEmbeddingGenerator<string, Embedding<float>> mockGenerator =
            Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        IAIProviderFactory providerFactory = Substitute.For<IAIProviderFactory>();
        providerFactory.ProviderName.Returns("OpenAI");
        providerFactory.CreateEmbeddingGenerator(workspace).Returns(mockGenerator);

        DefaultAIEmbeddingGeneratorFactory factory = new(_workspaceProvider, [providerFactory], _options);

        IEmbeddingGenerator<string, Embedding<float>> result =
            await factory.CreateAsync("test", TestContext.Current.CancellationToken);

        result.ShouldBe(mockGenerator);
    }

    [Fact]
    public async Task CreateAsync_NullWorkspaceName_UsesDefault()
    {
        AIWorkspace workspace = CreateWorkspace("default");
        _workspaceProvider.GetAsync("default", Arg.Any<CancellationToken>()).Returns(workspace);

        IEmbeddingGenerator<string, Embedding<float>> mockGenerator =
            Substitute.For<IEmbeddingGenerator<string, Embedding<float>>>();
        IAIProviderFactory providerFactory = Substitute.For<IAIProviderFactory>();
        providerFactory.ProviderName.Returns("OpenAI");
        providerFactory.CreateEmbeddingGenerator(workspace).Returns(mockGenerator);

        DefaultAIEmbeddingGeneratorFactory factory = new(_workspaceProvider, [providerFactory], _options);

        IEmbeddingGenerator<string, Embedding<float>> result =
            await factory.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(mockGenerator);
    }

    [Fact]
    public async Task CreateAsync_WorkspaceNotFound_ThrowsAIWorkspaceNotFoundException()
    {
        _workspaceProvider.GetAsync("missing", Arg.Any<CancellationToken>()).Returns((AIWorkspace?)null);

        DefaultAIEmbeddingGeneratorFactory factory = new(_workspaceProvider, [], _options);

        AIWorkspaceNotFoundException exception = await Should.ThrowAsync<AIWorkspaceNotFoundException>(
            () => factory.CreateAsync("missing", TestContext.Current.CancellationToken));

        exception.WorkspaceName.ShouldBe("missing");
    }

    [Fact]
    public async Task CreateAsync_ProviderNotRegistered_ThrowsAIProviderNotRegisteredException()
    {
        AIWorkspace workspace = CreateWorkspace(provider: "UnknownProvider");
        _workspaceProvider.GetAsync("test", Arg.Any<CancellationToken>()).Returns(workspace);

        DefaultAIEmbeddingGeneratorFactory factory = new(_workspaceProvider, [], _options);

        AIProviderNotRegisteredException exception = await Should.ThrowAsync<AIProviderNotRegisteredException>(
            () => factory.CreateAsync("test", TestContext.Current.CancellationToken));

        exception.ProviderName.ShouldBe("UnknownProvider");
    }

    [Fact]
    public async Task CreateAsync_ProviderReturnsNullGenerator_ThrowsInvalidOperationException()
    {
        AIWorkspace workspace = CreateWorkspace();
        _workspaceProvider.GetAsync("test", Arg.Any<CancellationToken>()).Returns(workspace);

        IAIProviderFactory providerFactory = Substitute.For<IAIProviderFactory>();
        providerFactory.ProviderName.Returns("OpenAI");
        providerFactory.CreateEmbeddingGenerator(workspace).Returns((IEmbeddingGenerator<string, Embedding<float>>?)null);

        DefaultAIEmbeddingGeneratorFactory factory = new(_workspaceProvider, [providerFactory], _options);

        await Should.ThrowAsync<InvalidOperationException>(
            () => factory.CreateAsync("test", TestContext.Current.CancellationToken));
    }
}
