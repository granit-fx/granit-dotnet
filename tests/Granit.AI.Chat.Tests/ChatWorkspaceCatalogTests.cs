using Granit.AI.Chat.Internal;
using Granit.AI.Chat.Settings;
using Granit.AI.Options;
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

    private ChatWorkspaceCatalog Catalog(string defaultWorkspace) =>
        new(_workspaceProvider, _capabilityResolver,
            Microsoft.Extensions.Options.Options.Create(new GranitAIOptions { DefaultWorkspace = defaultWorkspace }));

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

        // The host default is a chat-capable workspace, so 'Auto' is resolvable and offered first.
        IReadOnlyList<string> result = await Catalog("chat")
            .GetSelectableWorkspacesAsync(TestContext.Current.CancellationToken);

        result[0].ShouldBe(AIChatSettingNames.ReservedAutoWorkspace);
        result.ShouldContain("chat");
        result.ShouldContain("unknown"); // unknown capability => treated as chat-capable
        result.ShouldNotContain("embed"); // explicitly non-chat => excluded
    }

    [Fact]
    public async Task Omits_auto_when_default_workspace_is_not_chat_capable()
    {
        _workspaceProvider.GetAllAsync(Arg.Any<CancellationToken>()).Returns(
        [
            Workspace("chat", "gpt-4o"),
        ]);
        _capabilityResolver.ResolveAsync("OpenAI", "gpt-4o", Arg.Any<CancellationToken>())
            .Returns(new AIModelCapabilities { Chat = true });

        // Framework placeholder default ("default") matches no workspace => 'Auto' would 404, so omit it.
        IReadOnlyList<string> result = await Catalog("default")
            .GetSelectableWorkspacesAsync(TestContext.Current.CancellationToken);

        result.ShouldNotContain(AIChatSettingNames.ReservedAutoWorkspace);
        result.ShouldHaveSingleItem().ShouldBe("chat");
    }

    [Fact]
    public async Task Omits_auto_when_default_workspace_is_blank()
    {
        _workspaceProvider.GetAllAsync(Arg.Any<CancellationToken>()).Returns(
        [
            Workspace("chat", "gpt-4o"),
        ]);
        _capabilityResolver.ResolveAsync("OpenAI", "gpt-4o", Arg.Any<CancellationToken>())
            .Returns(new AIModelCapabilities { Chat = true });

        IReadOnlyList<string> result = await Catalog("  ")
            .GetSelectableWorkspacesAsync(TestContext.Current.CancellationToken);

        result.ShouldNotContain(AIChatSettingNames.ReservedAutoWorkspace);
        result.ShouldHaveSingleItem().ShouldBe("chat");
    }

    [Fact]
    public async Task Returns_empty_when_no_workspaces()
    {
        _workspaceProvider.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        // No workspaces => nothing chat-capable => 'Auto' cannot resolve => empty list.
        IReadOnlyList<string> result = await Catalog("general-chat")
            .GetSelectableWorkspacesAsync(TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }
}
