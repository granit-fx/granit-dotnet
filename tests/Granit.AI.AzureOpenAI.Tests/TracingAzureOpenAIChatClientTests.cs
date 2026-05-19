using System.Diagnostics;
using Granit.AI.AzureOpenAI.Diagnostics;
using Granit.AI.AzureOpenAI.Internal;
using Granit.AI.Tenancy;
using Microsoft.Extensions.AI;
using NSubstitute;
using Shouldly;

namespace Granit.AI.AzureOpenAI.Tests;

public sealed class TracingAzureOpenAIChatClientTests
{
    private static readonly AIProviderCredential TestCredential = new()
    {
        ApiKey = "test",
        Endpoint = "https://res.openai.azure.com",
        Scope = AIProviderCredentialScope.Host,
        BilledToTenantId = null,
    };

    private static (ActivityListener Listener, List<Activity> Started) ListenForSpans()
    {
        List<Activity> started = [];
        ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == AIAzureOpenAIActivitySource.Name,
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
            Usage = new UsageDetails { InputTokenCount = 4, OutputTokenCount = 6 },
        };
        inner.GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));

        TracingAzureOpenAIChatClient sut = new(inner, "gpt-4o", TestCredential);

        ChatResponse response = await sut.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "ping")],
            cancellationToken: TestContext.Current.CancellationToken);

        response.ShouldBe(expected);
        Activity activity = started.ShouldHaveSingleItem();
        activity.OperationName.ShouldBe(AIAzureOpenAIActivitySource.ChatOperation);
        activity.GetTagItem("gen_ai.system").ShouldBe(AIAzureOpenAIActivitySource.SystemTagValue);
        activity.GetTagItem("gen_ai.request.model").ShouldBe("gpt-4o");
        activity.GetTagItem("gen_ai.usage.input_tokens").ShouldBe(4);
        activity.GetTagItem("gen_ai.usage.output_tokens").ShouldBe(6);
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

        TracingAzureOpenAIChatClient sut = new(inner, "gpt-4o", TestCredential);

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "x")],
                cancellationToken: TestContext.Current.CancellationToken));

        Activity activity = started.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.GetTagItem("error.type").ShouldBe(typeof(InvalidOperationException).FullName);
    }

    [Fact]
    public void Dispose_DelegatesToInner()
    {
        IChatClient inner = Substitute.For<IChatClient>();
        TracingAzureOpenAIChatClient sut = new(inner, "gpt-4o", TestCredential);

        sut.Dispose();

        inner.Received(1).Dispose();
    }
}
