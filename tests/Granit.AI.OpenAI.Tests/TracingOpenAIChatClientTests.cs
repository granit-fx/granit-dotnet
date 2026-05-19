using System.Diagnostics;
using Granit.AI.OpenAI.Diagnostics;
using Granit.AI.OpenAI.Internal;
using Microsoft.Extensions.AI;
using NSubstitute;
using Shouldly;

namespace Granit.AI.OpenAI.Tests;

public sealed class TracingOpenAIChatClientTests
{
    private static (ActivityListener Listener, List<Activity> Started) ListenForSpans()
    {
        List<Activity> started = [];
        ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == AIOpenAIActivitySource.Name,
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

        TracingOpenAIChatClient sut = new(inner, "gpt-4o");

        ChatResponse response = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "ping")],
            cancellationToken: TestContext.Current.CancellationToken);

        response.ShouldBe(expected);
        Activity activity = started.ShouldHaveSingleItem();
        activity.OperationName.ShouldBe(AIOpenAIActivitySource.ChatOperation);
        activity.GetTagItem("gen_ai.system").ShouldBe(AIOpenAIActivitySource.SystemTagValue);
        activity.GetTagItem("gen_ai.request.model").ShouldBe("gpt-4o");
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

        TracingOpenAIChatClient sut = new(inner, "o3");

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "x")],
                cancellationToken: TestContext.Current.CancellationToken));

        Activity activity = started.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public void GetService_DelegatesToInner()
    {
        IChatClient inner = Substitute.For<IChatClient>();
        object sentinel = new();
        inner.GetService(typeof(string), "key").Returns(sentinel);

        TracingOpenAIChatClient sut = new(inner, "gpt-4o");

        sut.GetService(typeof(string), "key").ShouldBe(sentinel);
    }

    [Fact]
    public void Dispose_DelegatesToInner()
    {
        IChatClient inner = Substitute.For<IChatClient>();
        TracingOpenAIChatClient sut = new(inner, "gpt-4o");

        sut.Dispose();

        inner.Received(1).Dispose();
    }
}
