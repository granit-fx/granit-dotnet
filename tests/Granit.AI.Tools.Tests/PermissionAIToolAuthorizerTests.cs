using System.Text.Json;
using Granit.AI.Tools.Internal;
using Granit.Authorization;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Tools.Tests;

public sealed class PermissionAIToolAuthorizerTests
{
    private sealed class GatedTool(string name, string permission) : IAITool, IGatedAITool
    {
        public string Name => name;
        public string Description => "gated";
        public string RequiredPermission => permission;
        public JsonElement ParameterSchema => AIToolSchema.Empty;

        public ValueTask<AIToolResult> InvokeAsync(
            AIToolInvocationContext context, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(AIToolResult.Success("ok"));
    }

    private static PermissionAIToolAuthorizer Authorizer(params string[] granted)
    {
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.GetGrantedAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<string>>(
                [.. ci.Arg<IReadOnlyList<string>>().Where(granted.Contains)]));
        return new PermissionAIToolAuthorizer(checker);
    }

    [Fact]
    public async Task Ungated_tools_always_pass()
    {
        PermissionAIToolAuthorizer authorizer = Authorizer();
        IReadOnlyList<IAITool> tools = [new FakeAITool("query_data"), new FakeAITool("search")];

        IReadOnlyList<IAITool> result = await authorizer.FilterAuthorizedAsync(
            tools, TestContext.Current.CancellationToken);

        result.ShouldBe(tools);
    }

    [Fact]
    public async Task Gated_tool_is_kept_when_its_permission_is_granted()
    {
        PermissionAIToolAuthorizer authorizer = Authorizer("AI.ChatTools.Translate");
        IReadOnlyList<IAITool> tools = [new GatedTool("translate", "AI.ChatTools.Translate")];

        IReadOnlyList<IAITool> result = await authorizer.FilterAuthorizedAsync(
            tools, TestContext.Current.CancellationToken);

        result.ShouldHaveSingleItem().Name.ShouldBe("translate");
    }

    [Fact]
    public async Task Gated_tool_is_removed_when_its_permission_is_missing()
    {
        PermissionAIToolAuthorizer authorizer = Authorizer(); // nothing granted
        IReadOnlyList<IAITool> tools =
        [
            new FakeAITool("search"),
            new GatedTool("translate", "AI.ChatTools.Translate"),
        ];

        IReadOnlyList<IAITool> result = await authorizer.FilterAuthorizedAsync(
            tools, TestContext.Current.CancellationToken);

        result.ShouldHaveSingleItem().Name.ShouldBe("search");
    }

    [Fact]
    public async Task No_permission_check_runs_when_no_tool_is_gated()
    {
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        PermissionAIToolAuthorizer authorizer = new(checker);

        await authorizer.FilterAuthorizedAsync([new FakeAITool("search")], TestContext.Current.CancellationToken);

        await checker.DidNotReceive().GetGrantedAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }
}
