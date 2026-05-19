using Granit.AI.Exceptions;
using Granit.AI.Internal;
using Granit.AI.Options;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Tests;

public sealed class DefaultAIChatClientFactoryTests
{
    private readonly IAIWorkspaceProvider _workspaceProvider = Substitute.For<IAIWorkspaceProvider>();
    private readonly IOptions<GranitAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new GranitAIOptions { DefaultWorkspace = "default" });

    private static AIWorkspace CreateWorkspace(string name = "test", string provider = "OpenAI") =>
        new()
        {
            Name = name,
            Provider = provider,
            Model = "gpt-4o",
        };

    [Fact]
    public async Task CreateAsync_KnownWorkspaceAndProvider_ReturnsChatClient()
    {
        AIWorkspace workspace = CreateWorkspace();
        _workspaceProvider.GetAsync("test", Arg.Any<CancellationToken>()).Returns(workspace);

        IChatClient mockClient = Substitute.For<IChatClient>();
        IAIProviderFactory providerFactory = Substitute.For<IAIProviderFactory>();
        providerFactory.ProviderName.Returns("OpenAI");
        providerFactory.CreateChatClientAsync(workspace, Arg.Any<CancellationToken>()).Returns(mockClient);

        var factory = new DefaultAIChatClientFactory(_workspaceProvider, [providerFactory], _options);

        IChatClient result = await factory.CreateAsync("test", TestContext.Current.CancellationToken);

        result.ShouldBe(mockClient);
    }

    [Fact]
    public async Task CreateAsync_NullWorkspaceName_UsesDefault()
    {
        AIWorkspace workspace = CreateWorkspace("default");
        _workspaceProvider.GetAsync("default", Arg.Any<CancellationToken>()).Returns(workspace);

        IChatClient mockClient = Substitute.For<IChatClient>();
        IAIProviderFactory providerFactory = Substitute.For<IAIProviderFactory>();
        providerFactory.ProviderName.Returns("OpenAI");
        providerFactory.CreateChatClientAsync(workspace, Arg.Any<CancellationToken>()).Returns(mockClient);

        var factory = new DefaultAIChatClientFactory(_workspaceProvider, [providerFactory], _options);

        IChatClient result = await factory.CreateAsync(cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBe(mockClient);
    }

    [Fact]
    public async Task CreateAsync_WorkspaceNotFound_ThrowsAIWorkspaceNotFoundException()
    {
        _workspaceProvider.GetAsync("missing", Arg.Any<CancellationToken>()).Returns((AIWorkspace?)null);

        var factory = new DefaultAIChatClientFactory(_workspaceProvider, [], _options);

        AIWorkspaceNotFoundException exception = await Should.ThrowAsync<AIWorkspaceNotFoundException>(
            () => factory.CreateAsync("missing", TestContext.Current.CancellationToken));

        exception.WorkspaceName.ShouldBe("missing");
    }

    [Fact]
    public async Task CreateAsync_ProviderNotRegistered_ThrowsAIProviderNotRegisteredException()
    {
        AIWorkspace workspace = CreateWorkspace(provider: "UnknownProvider");
        _workspaceProvider.GetAsync("test", Arg.Any<CancellationToken>()).Returns(workspace);

        var factory = new DefaultAIChatClientFactory(_workspaceProvider, [], _options);

        AIProviderNotRegisteredException exception = await Should.ThrowAsync<AIProviderNotRegisteredException>(
            () => factory.CreateAsync("test", TestContext.Current.CancellationToken));

        exception.ProviderName.ShouldBe("UnknownProvider");
    }
}
