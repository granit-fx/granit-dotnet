using Granit.AI.Chat.Internal;
using Granit.AI.Chat.Settings;
using Granit.AI.Workspaces;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Chat.Tests;

public sealed class ChatWorkspaceCatalogTests
{
    private readonly IAIWorkspaceProvider _workspaceProvider = Substitute.For<IAIWorkspaceProvider>();
    private readonly IAIWorkspaceCapabilityResolver _capabilityResolver = Substitute.For<IAIWorkspaceCapabilityResolver>();

    private static AIWorkspace Workspace(string name, string model) =>
        new() { Name = name, Provider = "OpenAI", Model = model };

    [Fact]
    public async Task Lists_auto_first_then_chat_capable_workspaces_excluding_non_chat()
    {
        _workspaceProvider.GetAllAsync(Arg.Any<CancellationToken>()).Returns(
        [
            Workspace("chat", "gpt-4o"),
            Workspace("embed", "text-embedding-3"),
            Workspace("unknown", "mystery"),
        ]);
        _capabilityResolver.ResolveAsync("OpenAI", "gpt-4o", Arg.Any<CancellationToken>())
            .Returns(new AIModelCapabilities { Chat = true });
        _capabilityResolver.ResolveAsync("OpenAI", "text-embedding-3", Arg.Any<CancellationToken>())
            .Returns(new AIModelCapabilities { Chat = false });
        _capabilityResolver.ResolveAsync("OpenAI", "mystery", Arg.Any<CancellationToken>())
            .Returns((AIModelCapabilities?)null);

        var catalog = new ChatWorkspaceCatalog(_workspaceProvider, _capabilityResolver);
        IReadOnlyList<string> result = await catalog.GetSelectableWorkspacesAsync(TestContext.Current.CancellationToken);

        result[0].ShouldBe(AIChatSettingNames.ReservedAutoWorkspace);
        result.ShouldContain("chat");
        result.ShouldContain("unknown"); // unknown capability => treated as chat-capable
        result.ShouldNotContain("embed"); // explicitly non-chat => excluded
    }

    [Fact]
    public async Task Returns_only_auto_when_no_workspaces()
    {
        _workspaceProvider.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
        var catalog = new ChatWorkspaceCatalog(_workspaceProvider, _capabilityResolver);

        IReadOnlyList<string> result = await catalog.GetSelectableWorkspacesAsync(TestContext.Current.CancellationToken);

        result.ShouldHaveSingleItem().ShouldBe(AIChatSettingNames.ReservedAutoWorkspace);
    }
}
