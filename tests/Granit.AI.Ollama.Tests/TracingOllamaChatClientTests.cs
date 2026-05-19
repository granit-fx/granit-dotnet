using System.Diagnostics;
using Granit.AI.Ollama.Diagnostics;
using Granit.AI.Ollama.Internal;
using Microsoft.Extensions.AI;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Ollama.Tests;

public sealed class TracingOllamaChatClientTests
{
    private static (ActivityListener Listener, List<Activity> Started) ListenForSpans()
    {
        List<Activity> started = [];
        ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == AIOllamaActivitySource.Name,
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
            Usage = new UsageDetails { InputTokenCount = 2, OutputTokenCount = 3 },
        };
        inner.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));

        TracingOllamaChatClient sut = new(inner, "llama3.2");

        ChatResponse response = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "ping")],
            cancellationToken: TestContext.Current.CancellationToken);

        response.ShouldBe(expected);
        Activity activity = started.ShouldHaveSingleItem();
        activity.OperationName.ShouldBe(AIOllamaActivitySource.ChatOperation);
        activity.GetTagItem("gen_ai.system").ShouldBe(AIOllamaActivitySource.SystemTagValue);
        activity.GetTagItem("gen_ai.request.model").ShouldBe("llama3.2");
        activity.GetTagItem("gen_ai.usage.input_tokens").ShouldBe(2);
        activity.GetTagItem("gen_ai.usage.output_tokens").ShouldBe(3);
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

        TracingOllamaChatClient sut = new(inner, "phi3");

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "x")],
                cancellationToken: TestContext.Current.CancellationToken));

        Activity activity = started.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public void GetService_ForOwnType_ReturnsSelf()
    {
        IChatClient inner = Substitute.For<IChatClient>();
        TracingOllamaChatClient sut = new(inner, "llama3.2");

        sut.GetService(typeof(TracingOllamaChatClient)).ShouldBe(sut);
    }

    [Fact]
    public void GetService_DelegatesToInner_ForUnknownType()
    {
        IChatClient inner = Substitute.For<IChatClient>();
        object sentinel = new();
        inner.GetService(typeof(string), null).Returns(sentinel);

        TracingOllamaChatClient sut = new(inner, "llama3.2");

        sut.GetService(typeof(string)).ShouldBe(sentinel);
    }

    [Fact]
    public void Dispose_DelegatesToInner()
    {
        IChatClient inner = Substitute.For<IChatClient>();
        TracingOllamaChatClient sut = new(inner, "llama3.2");

        sut.Dispose();

        inner.Received(1).Dispose();
    }
}
