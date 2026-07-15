using System.Diagnostics.Metrics;
using Granit.AI.Diagnostics;
using Granit.AI.Internal;
using Granit.AI.Workspaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Granit.AI.Tests;

public sealed class UsageTrackingChatClientTests
{
    private readonly IChatClient _inner = Substitute.For<IChatClient>();
    private readonly IAIUsageTracker _usageTracker = Substitute.For<IAIUsageTracker>();
    private readonly IAIUsageRecordFactory _usageRecordFactory = Substitute.For<IAIUsageRecordFactory>();

    private static readonly AIWorkspace Workspace = new()
    {
        Key = "ws",
        Provider = "OpenAI",
        Model = "gpt-4o",
    };

    public UsageTrackingChatClientTests() =>
        _usageRecordFactory
            .Create(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<TimeSpan?>())
            .Returns(callInfo => new AIUsageRecord
            {
                Id = Guid.NewGuid(),
                WorkspaceName = callInfo.ArgAt<string>(0),
                Provider = callInfo.ArgAt<string>(1),
                Model = callInfo.ArgAt<string>(2),
                InputTokens = callInfo.ArgAt<int>(3),
                OutputTokens = callInfo.ArgAt<int>(4),
                Timestamp = DateTimeOffset.UtcNow,
            });

    private UsageTrackingChatClient CreateSut() =>
        new(_inner, "ws", Workspace, _usageTracker, _usageRecordFactory,
            new AIMetrics(new StubMeterFactory()), TimeProvider.System,
            NullLogger<UsageTrackingChatClient>.Instance);

    [Fact]
    public async Task GetResponseAsync_WithUsage_StampsOneRecord()
    {
        _inner
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "hi"))
            {
                Usage = new UsageDetails { InputTokenCount = 12, OutputTokenCount = 7 },
            });

        await CreateSut().GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            cancellationToken: TestContext.Current.CancellationToken);

        await _usageTracker.Received(1).RecordAsync(
            Arg.Is<AIUsageRecord>(r => r.InputTokens == 12 && r.OutputTokens == 7 && r.WorkspaceName == "ws"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetResponseAsync_WithoutUsage_StampsNothing()
    {
        _inner
            .GetResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "hi")));

        await CreateSut().GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            cancellationToken: TestContext.Current.CancellationToken);

        await _usageTracker.DidNotReceive().RecordAsync(Arg.Any<AIUsageRecord>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStreamingResponseAsync_KeepsLastUsageContent_AndStampsOnce()
    {
        _inner
            .GetStreamingResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Stream(
                Text("a"),
                Usage(1, 1),
                Text("b"),
                Usage(20, 9)));

        List<ChatResponseUpdate> updates = [];
        await foreach (ChatResponseUpdate update in CreateSut().GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "hello")],
            cancellationToken: TestContext.Current.CancellationToken))
        {
            updates.Add(update);
        }

        updates.Count.ShouldBe(4);
        await _usageTracker.Received(1).RecordAsync(
            Arg.Is<AIUsageRecord>(r => r.InputTokens == 20 && r.OutputTokens == 9),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStreamingResponseAsync_AbandonedEnumerator_StillStampsViaFinally()
    {
        _inner
            .GetStreamingResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Stream(Usage(5, 3), Text("a"), Text("b")));

        IAsyncEnumerator<ChatResponseUpdate> enumerator = CreateSut()
            .GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "hello")],
                cancellationToken: TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        (await enumerator.MoveNextAsync()).ShouldBeTrue();
        await enumerator.DisposeAsync(); // abandon mid-stream

        await _usageTracker.Received(1).RecordAsync(
            Arg.Is<AIUsageRecord>(r => r.InputTokens == 5 && r.OutputTokens == 3),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetStreamingResponseAsync_TrackerReceivesLiveToken_NotTheCallersCancelledOne()
    {
        using CancellationTokenSource callerCts = new();

        CancellationToken received = default;
        _usageTracker
            .RecordAsync(Arg.Any<AIUsageRecord>(), Arg.Do<CancellationToken>(t => received = t))
            .Returns(Task.CompletedTask);

        _inner
            .GetStreamingResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Stream(Usage(2, 2)));

        IAsyncEnumerator<ChatResponseUpdate> enumerator = CreateSut()
            .GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hello")], cancellationToken: callerCts.Token)
            .GetAsyncEnumerator(CancellationToken.None);

        (await enumerator.MoveNextAsync()).ShouldBeTrue();
        callerCts.Cancel(); // the client aborted — the usage write must still run on a live token
        await enumerator.DisposeAsync();

        await _usageTracker.Received(1).RecordAsync(Arg.Any<AIUsageRecord>(), Arg.Any<CancellationToken>());
        received.IsCancellationRequested.ShouldBeFalse();
    }

    [Fact]
    public async Task GetStreamingResponseAsync_TrackerFailure_IsLoggedNotThrown()
    {
        _usageTracker
            .RecordAsync(Arg.Any<AIUsageRecord>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("sink down"));

        _inner
            .GetStreamingResponseAsync(Arg.Any<IEnumerable<ChatMessage>>(), Arg.Any<ChatOptions?>(), Arg.Any<CancellationToken>())
            .Returns(Stream(Text("a"), Usage(1, 1)));

        List<ChatResponseUpdate> updates = [];
        await Should.NotThrowAsync(async () =>
        {
            await foreach (ChatResponseUpdate update in CreateSut().GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "hello")],
                cancellationToken: TestContext.Current.CancellationToken))
            {
                updates.Add(update);
            }
        });

        updates.Count.ShouldBe(2);
    }

    private static ChatResponseUpdate Text(string text) =>
        new(ChatRole.Assistant, text);

    private static ChatResponseUpdate Usage(int input, int output) =>
        new(ChatRole.Assistant, [new UsageContent(new UsageDetails
        {
            InputTokenCount = input,
            OutputTokenCount = output,
        })]);

    private static async IAsyncEnumerable<ChatResponseUpdate> Stream(params ChatResponseUpdate[] updates)
    {
        foreach (ChatResponseUpdate update in updates)
        {
            await Task.Yield();
            yield return update;
        }
    }

    private sealed class StubMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);

        public void Dispose() { }
    }
}
