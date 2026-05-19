using System.Diagnostics;
using Granit.AI.Anthropic.Diagnostics;
using Granit.AI.Anthropic.Internal;
using Granit.AI.Tenancy;
using Microsoft.Extensions.AI;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Anthropic.Tests;

public sealed class TracingAnthropicChatClientTests
{
    private static readonly AIProviderCredential TestCredential = new()
    {
        ApiKey = "sk-ant-test",
        Scope = AIProviderCredentialScope.Host,
        BilledToTenantId = null,
    };

    private static (ActivityListener Listener, List<Activity> Started) ListenForSpans()
    {
        List<Activity> started = [];
        ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == AIAnthropicActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = a => started.Add(a),
        };
        ActivitySource.AddActivityListener(listener);
        return (listener, started);
    }

    [Fact]
    public async Task GetResponseAsync_EmitsActivityWithGenAITags()
    {
        (ActivityListener listener, List<Activity> started) = ListenForSpans();
        using ActivityListener _ = listener;

        IChatClient inner = Substitute.For<IChatClient>();
        ChatResponse expected = new(new ChatMessage(ChatRole.Assistant, "hi"))
        {
            Usage = new UsageDetails { InputTokenCount = 7, OutputTokenCount = 11 },
        };
        inner.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));

        TracingAnthropicChatClient sut = new(inner, "claude-sonnet-4-6", TestCredential);

        ChatResponse response = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "ping")],
            cancellationToken: TestContext.Current.CancellationToken);

        response.ShouldBe(expected);
        Activity activity = started.ShouldHaveSingleItem();
        activity.OperationName.ShouldBe(AIAnthropicActivitySource.ChatOperation);
        activity.GetTagItem("gen_ai.system").ShouldBe(AIAnthropicActivitySource.SystemTagValue);
        activity.GetTagItem("gen_ai.request.model").ShouldBe("claude-sonnet-4-6");
        activity.GetTagItem("gen_ai.usage.input_tokens").ShouldBe(7);
        activity.GetTagItem("gen_ai.usage.output_tokens").ShouldBe(11);
        activity.Status.ShouldNotBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task GetResponseAsync_OnException_MarksActivityAsError()
    {
        (ActivityListener listener, List<Activity> started) = ListenForSpans();
        using ActivityListener _ = listener;

        IChatClient inner = Substitute.For<IChatClient>();
        inner.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns<Task<ChatResponse>>(_ => throw new InvalidOperationException("boom"));

        TracingAnthropicChatClient sut = new(inner, "claude-haiku-4-5", TestCredential);

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "x")],
                cancellationToken: TestContext.Current.CancellationToken));

        Activity activity = started.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_EmitsActivityAndAggregatesUsage()
    {
        (ActivityListener listener, List<Activity> started) = ListenForSpans();
        using ActivityListener _ = listener;

        IChatClient inner = Substitute.For<IChatClient>();
        inner.GetStreamingResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(ProduceStream());

        TracingAnthropicChatClient sut = new(inner, "claude-sonnet-4-6", TestCredential);

        List<ChatResponseUpdate> received = [];
        await foreach (ChatResponseUpdate update in sut.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "y")],
            cancellationToken: TestContext.Current.CancellationToken))
        {
            received.Add(update);
        }

        received.Count.ShouldBe(3);
        Activity activity = started.ShouldHaveSingleItem();
        activity.OperationName.ShouldBe(AIAnthropicActivitySource.ChatStreamOperation);
        activity.GetTagItem("gen_ai.usage.input_tokens").ShouldBe(3);
        activity.GetTagItem("gen_ai.usage.output_tokens").ShouldBe(5);

        static async IAsyncEnumerable<ChatResponseUpdate> ProduceStream()
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, "he");
            yield return new ChatResponseUpdate(ChatRole.Assistant, "llo");
            await Task.Yield();
            yield return new ChatResponseUpdate
            {
                Contents = [new UsageContent(new UsageDetails { InputTokenCount = 3, OutputTokenCount = 5 })],
            };
        }
    }

    [Fact]
    public void GetService_DelegatesToInner()
    {
        IChatClient inner = Substitute.For<IChatClient>();
        object sentinel = new();
        inner.GetService(typeof(string), "key").Returns(sentinel);

        TracingAnthropicChatClient sut = new(inner, "claude-sonnet-4-6", TestCredential);

        sut.GetService(typeof(string), "key").ShouldBe(sentinel);
    }

    [Fact]
    public void Dispose_DelegatesToInner()
    {
        IChatClient inner = Substitute.For<IChatClient>();
        TracingAnthropicChatClient sut = new(inner, "claude-sonnet-4-6", TestCredential);

        sut.Dispose();

        inner.Received(1).Dispose();
    }
}
