using Granit.AI.Mcp.Internal;
using Granit.AI.Mcp.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;
using NSubstitute;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.AI.Mcp.Tests;

public sealed class McpSamplingChatClientAdapterTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();

    private McpSamplingChatClientAdapter CreateAdapter(GranitAIMcpOptions? opts = null)
    {
        GranitAIMcpOptions config = opts ?? new GranitAIMcpOptions { EnableSampling = true };
        SamplingGuard guard = new(
            MsOptions.Create(config),
            TimeProvider.System,
            NullLogger<SamplingGuard>.Instance);

        return new McpSamplingChatClientAdapter(
            _chatClientFactory,
            guard,
            MsOptions.Create(config),
            NullLogger<McpSamplingChatClientAdapter>.Instance);
    }

    [Fact]
    public async Task HandleSamplingAsync_WhenDisabled_ShouldReturnRejection()
    {
        McpSamplingChatClientAdapter sut = CreateAdapter(new GranitAIMcpOptions { EnableSampling = false });
        CreateMessageRequestParams request = CreateRequest("Hello", maxTokens: 100);

        CreateMessageResult result = await sut.HandleSamplingAsync(request, TestContext.Current.CancellationToken);

        result.Role.ShouldBe(Role.Assistant);
        result.Model.ShouldBe("none");
        TextContentBlock text = result.Content.OfType<TextContentBlock>().Single();
        text.Text.ShouldContain("rejected");
        text.Text.ShouldContain("disabled");
    }

    [Fact]
    public async Task HandleSamplingAsync_WhenEnabled_ShouldForwardToWorkspace()
    {
        IChatClient mockClient = Substitute.For<IChatClient>();
        mockClient.GetResponseAsync(
                Arg.Any<IList<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse([new ChatMessage(ChatRole.Assistant, "World")])
            {
                ModelId = "gpt-4o",
            });

        _chatClientFactory
            .CreateAsync("default", Arg.Any<CancellationToken>())
            .Returns(mockClient);

        McpSamplingChatClientAdapter sut = CreateAdapter();
        CreateMessageRequestParams request = CreateRequest("Hello", maxTokens: 100);

        CreateMessageResult result = await sut.HandleSamplingAsync(request, TestContext.Current.CancellationToken);

        result.Role.ShouldBe(Role.Assistant);
        result.Model.ShouldBe("gpt-4o");
        TextContentBlock text = result.Content.OfType<TextContentBlock>().Single();
        text.Text.ShouldBe("World");
    }

    [Fact]
    public async Task HandleSamplingAsync_WhenTokensExceedLimit_ShouldReject()
    {
        McpSamplingChatClientAdapter sut = CreateAdapter(new GranitAIMcpOptions
        {
            EnableSampling = true,
            SamplingMaxTokensPerRequest = 500,
        });
        CreateMessageRequestParams request = CreateRequest("Hello", maxTokens: 5000);

        CreateMessageResult result = await sut.HandleSamplingAsync(request, TestContext.Current.CancellationToken);

        result.Model.ShouldBe("none");
        TextContentBlock text = result.Content.OfType<TextContentBlock>().Single();
        text.Text.ShouldContain("rejected");
        text.Text.ShouldContain("5000");
    }

    private static CreateMessageRequestParams CreateRequest(string text, int maxTokens) =>
        new()
        {
            Messages = [new SamplingMessage { Role = Role.User, Content = [new TextContentBlock { Text = text }] }],
            MaxTokens = maxTokens,
        };
}
