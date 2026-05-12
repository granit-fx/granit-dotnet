using System.Text.Json;
using System.Threading.Channels;
using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.Dashboards.Push.Internal;
using Granit.Dashboards.Push.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Push.Tests.Internal;

/// <summary>
/// Pins the <c>Last-Event-ID</c> resume contract from ADR-043 §5: ring buffer
/// drops oldest on overflow, fresh subscribers get no replay, reconnecting
/// subscribers within the ring window get the missed envelopes back, and a
/// client behind the oldest ring entry surfaces <see cref="SubscriptionResult.ResumeFailed"/>
/// = <see langword="true"/> so the SSE handler can emit
/// <c>event: resume-failed</c> and the frontend re-pulls the seed.
/// </summary>
public sealed class WidgetPushHubResumeTests
{
    private static readonly JsonElement SamplePayload = JsonSerializer.SerializeToElement(new { value = 42 });
    private static readonly DateTimeOffset EmittedAt = new(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Subscribe_FreshClient_NoLastEventId_NoReplay()
    {
        InMemoryWidgetPushHub hub = NewHub(ringCapacity: 100);

        var channel = Channel.CreateUnbounded<WidgetPushMessage>();
        SubscriptionResult result = hub.Subscribe(
            tenantId: Guid.NewGuid(),
            dashboardId: Guid.NewGuid(),
            lastEventId: null,
            writer: channel.Writer);
        using IDisposable _ = result.Handle;

        result.ResumeFailed.ShouldBeFalse();
        result.Replay.ShouldBeEmpty();
        result.ServerCursor.ShouldBe(0L);                                    // no envelopes yet
    }

    [Fact]
    public async Task PublishSnapshot_AssignsMonotonicStreamCursor_PerStream()
    {
        InMemoryWidgetPushHub hub = NewHub();
        var tenant = Guid.NewGuid();
        var dashboard = Guid.NewGuid();

        var channel = Channel.CreateUnbounded<WidgetPushMessage>();
        using IDisposable _ = hub.Subscribe(tenant, dashboard, lastEventId: null, channel.Writer).Handle;

        // Three different widget instances on the same dashboard — stream cursor
        // increments per-(tenant, dashboard), not per-widget.
        for (int i = 0; i < 3; i++)
        {
            await hub.PublishSnapshotAsync(
                tenant, dashboard, Guid.NewGuid(),
                "Kpi", requiredPermission: null, SamplePayload, EmittedAt, RefreshHint.Realtime,
                cancellationToken: TestContext.Current.CancellationToken);
        }

        long[] cursors =
        [
            ReadOne(channel).StreamCursor,
            ReadOne(channel).StreamCursor,
            ReadOne(channel).StreamCursor,
        ];

        cursors.ShouldBe([1L, 2L, 3L]);
    }

    [Fact]
    public async Task Subscribe_WithLastEventId_InRange_ReplaysMissedEnvelopes()
    {
        InMemoryWidgetPushHub hub = NewHub();
        var tenant = Guid.NewGuid();
        var dashboard = Guid.NewGuid();

        // Phase 1: a first client publishes, then disconnects after cursor=2.
        var live = Channel.CreateUnbounded<WidgetPushMessage>();
        IDisposable initial = hub.Subscribe(tenant, dashboard, lastEventId: null, live.Writer).Handle;
        for (int i = 0; i < 5; i++)
        {
            await hub.PublishSnapshotAsync(
                tenant, dashboard, Guid.NewGuid(),
                "Kpi", requiredPermission: null, SamplePayload, EmittedAt, RefreshHint.Realtime,
                cancellationToken: TestContext.Current.CancellationToken);
        }
        initial.Dispose();

        // Phase 2: client reconnects with Last-Event-ID=2 — expects cursors 3, 4, 5.
        var resumed = Channel.CreateUnbounded<WidgetPushMessage>();
        SubscriptionResult result = hub.Subscribe(tenant, dashboard, lastEventId: 2, resumed.Writer);
        using IDisposable _ = result.Handle;

        result.ResumeFailed.ShouldBeFalse();
        result.Replay.Select(m => m.StreamCursor).ShouldBe([3L, 4L, 5L]);
    }

    [Fact]
    public async Task Subscribe_WithLastEventId_FullyCaughtUp_NoReplay()
    {
        InMemoryWidgetPushHub hub = NewHub();
        var tenant = Guid.NewGuid();
        var dashboard = Guid.NewGuid();

        var live = Channel.CreateUnbounded<WidgetPushMessage>();
        IDisposable initial = hub.Subscribe(tenant, dashboard, lastEventId: null, live.Writer).Handle;
        for (int i = 0; i < 3; i++)
        {
            await hub.PublishSnapshotAsync(
                tenant, dashboard, Guid.NewGuid(),
                "Kpi", requiredPermission: null, SamplePayload, EmittedAt, RefreshHint.Realtime,
                cancellationToken: TestContext.Current.CancellationToken);
        }
        initial.Dispose();

        // Client reconnects already up to cursor=3 — nothing to replay.
        var resumed = Channel.CreateUnbounded<WidgetPushMessage>();
        SubscriptionResult result = hub.Subscribe(tenant, dashboard, lastEventId: 3, resumed.Writer);
        using IDisposable _ = result.Handle;

        result.ResumeFailed.ShouldBeFalse();
        result.Replay.ShouldBeEmpty();
    }

    [Fact]
    public async Task Subscribe_WithLastEventId_BehindRingOldest_ReportsResumeFailed()
    {
        // Tiny ring (capacity 3) so we can overflow it deterministically.
        InMemoryWidgetPushHub hub = NewHub(ringCapacity: 3);
        var tenant = Guid.NewGuid();
        var dashboard = Guid.NewGuid();

        var live = Channel.CreateUnbounded<WidgetPushMessage>();
        IDisposable initial = hub.Subscribe(tenant, dashboard, lastEventId: null, live.Writer).Handle;

        // Publish 10 envelopes — ring keeps cursors 8, 9, 10 only.
        for (int i = 0; i < 10; i++)
        {
            await hub.PublishSnapshotAsync(
                tenant, dashboard, Guid.NewGuid(),
                "Kpi", requiredPermission: null, SamplePayload, EmittedAt, RefreshHint.Realtime,
                cancellationToken: TestContext.Current.CancellationToken);
        }
        initial.Dispose();

        // Client reconnects with Last-Event-ID=2 — far behind the ring's oldest
        // (cursor 8). Resume must fail so the SSE handler can emit
        // `event: resume-failed` and the frontend re-fetches the seed.
        var resumed = Channel.CreateUnbounded<WidgetPushMessage>();
        SubscriptionResult result = hub.Subscribe(tenant, dashboard, lastEventId: 2, resumed.Writer);
        using IDisposable _ = result.Handle;

        result.ResumeFailed.ShouldBeTrue();
        result.Replay.ShouldBeEmpty();
        result.ServerCursor.ShouldBe(10L);
    }

    [Fact]
    public async Task Ring_DropsOldest_OnOverflow()
    {
        InMemoryWidgetPushHub hub = NewHub(ringCapacity: 3);
        var tenant = Guid.NewGuid();
        var dashboard = Guid.NewGuid();

        // Drive the cursor past the ring's capacity.
        var warmup = Channel.CreateUnbounded<WidgetPushMessage>();
        IDisposable initial = hub.Subscribe(tenant, dashboard, lastEventId: null, warmup.Writer).Handle;
        for (int i = 0; i < 7; i++)
        {
            await hub.PublishSnapshotAsync(
                tenant, dashboard, Guid.NewGuid(),
                "Kpi", requiredPermission: null, SamplePayload, EmittedAt, RefreshHint.Realtime,
                cancellationToken: TestContext.Current.CancellationToken);
        }
        initial.Dispose();

        // The ring holds the last 3 cursors only — replay from id 4 returns 5, 6, 7.
        var probe = Channel.CreateUnbounded<WidgetPushMessage>();
        SubscriptionResult result = hub.Subscribe(tenant, dashboard, lastEventId: 4, probe.Writer);
        using IDisposable _ = result.Handle;

        result.ResumeFailed.ShouldBeFalse();
        result.Replay.Select(m => m.StreamCursor).ShouldBe([5L, 6L, 7L]);
    }

    [Fact]
    public async Task Subscribe_AfterDispatchStarts_ReceivesLiveEnvelopesAfterReplay()
    {
        // Atomic guarantee: the hub locks ring + subscriber registration together,
        // so a publish that races with a Subscribe call lands EITHER in the replay
        // snapshot OR on the live channel — never both, never neither. This test
        // exercises the happy path where the publisher fires before subscribe (so
        // the message is in the replay) and again after (so the message is on the
        // channel) and verifies no duplicate.
        InMemoryWidgetPushHub hub = NewHub();
        var tenant = Guid.NewGuid();
        var dashboard = Guid.NewGuid();

        // Pre-publish one envelope without any subscriber — only the ring buffers it.
        await hub.PublishSnapshotAsync(
            tenant, dashboard, Guid.NewGuid(),
            "Kpi", requiredPermission: null, SamplePayload, EmittedAt, RefreshHint.Realtime,
            cancellationToken: TestContext.Current.CancellationToken);

        var channel = Channel.CreateUnbounded<WidgetPushMessage>();
        SubscriptionResult result = hub.Subscribe(tenant, dashboard, lastEventId: 0, channel.Writer);
        using IDisposable _ = result.Handle;

        // Replay carries cursor=1.
        result.Replay.Select(m => m.StreamCursor).ShouldBe([1L]);

        // Now publish a second envelope — lands on the live channel only.
        await hub.PublishSnapshotAsync(
            tenant, dashboard, Guid.NewGuid(),
            "Kpi", requiredPermission: null, SamplePayload, EmittedAt, RefreshHint.Realtime,
            cancellationToken: TestContext.Current.CancellationToken);

        WidgetPushMessage live = ReadOne(channel);
        live.StreamCursor.ShouldBe(2L);
        channel.Reader.TryRead(out WidgetPushMessage? extra).ShouldBeFalse(); // no second item — no duplicate
        extra.ShouldBeNull();
    }

    private static WidgetPushMessage ReadOne(Channel<WidgetPushMessage> channel)
    {
        channel.Reader.TryRead(out WidgetPushMessage? message).ShouldBeTrue();
        return message!;
    }

    private static InMemoryWidgetPushHub NewHub(int ringCapacity = 100)
    {
        IOptions<DashboardsPushOptions> options = Microsoft.Extensions.Options.Options.Create(new DashboardsPushOptions
        {
            RingBufferCapacity = ringCapacity,
        });
        return new InMemoryWidgetPushHub(new WidgetPushSequenceAllocator(), options);
    }
}
