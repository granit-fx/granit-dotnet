using System.Text.Json;
using System.Threading.Channels;
using Granit.Analytics.Metrics;
using Granit.Dashboards.Push.Internal;
using Granit.Dashboards.Push.Options;
using Granit.Dashboards.Rendering;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Push.Tests.Internal;

/// <summary>
/// Pins the in-process pub/sub semantics of <see cref="InMemoryWidgetPushHub"/>:
/// publish dispatches to every subscriber on the matching <c>(tenantId, dashboardId)</c>
/// stream, unrelated streams stay quiet, unsubscribe stops delivery, sequences come
/// from the allocator and increment monotonically.
/// </summary>
public sealed class InMemoryWidgetPushHubTests
{
    private static readonly JsonElement SamplePayload = JsonSerializer.SerializeToElement(new { value = 42 });
    private static readonly DateTimeOffset EmittedAt = new(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PublishSnapshot_DispatchesToMatchingSubscriber()
    {
        InMemoryWidgetPushHub hub = NewHub();
        var tenant = Guid.NewGuid();
        var dashboard = Guid.NewGuid();
        var widget = Guid.NewGuid();

        var channel = Channel.CreateUnbounded<WidgetPushMessage>();
        using IDisposable _ = hub.Subscribe(tenant, dashboard, lastEventId: null, channel.Writer).Handle;

        await hub.PublishSnapshotAsync(
            tenant, dashboard, widget,
            widgetType: "Kpi",
            requiredPermission: null,
            snapshot: SamplePayload,
            emittedAt: EmittedAt,
            refreshHint: RefreshHint.Realtime,
            cancellationToken: TestContext.Current.CancellationToken);

        channel.Reader.TryRead(out WidgetPushMessage? message).ShouldBeTrue();
        message.ShouldNotBeNull();
        message!.TenantId.ShouldBe(tenant);
        message.DashboardId.ShouldBe(dashboard);
        message.WidgetInstanceId.ShouldBe(widget);
        message.Envelope.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        message.Envelope.WidgetType.ShouldBe("Kpi");
        message.Envelope.Sequence.ShouldBe(1L);                              // first push for this widget
        message.Envelope.RefreshHint.ShouldBe(RefreshHint.Realtime);
        message.Envelope.Snapshot.ShouldNotBeNull();
    }

    [Fact]
    public async Task PublishSnapshot_FanOutsToAllSubscribersOfSameStream()
    {
        InMemoryWidgetPushHub hub = NewHub();
        var tenant = Guid.NewGuid();
        var dashboard = Guid.NewGuid();

        var a = Channel.CreateUnbounded<WidgetPushMessage>();
        var b = Channel.CreateUnbounded<WidgetPushMessage>();
        using IDisposable _ = hub.Subscribe(tenant, dashboard, lastEventId: null, a.Writer).Handle;
        using IDisposable __ = hub.Subscribe(tenant, dashboard, lastEventId: null, b.Writer).Handle;

        await hub.PublishSnapshotAsync(
            tenant, dashboard, Guid.NewGuid(),
            "Kpi", requiredPermission: null, SamplePayload, EmittedAt, RefreshHint.Realtime,
            cancellationToken: TestContext.Current.CancellationToken);

        a.Reader.TryRead(out WidgetPushMessage? _).ShouldBeTrue();
        b.Reader.TryRead(out WidgetPushMessage? _).ShouldBeTrue();
    }

    [Fact]
    public async Task PublishSnapshot_DoesNotLeakAcrossDashboards()
    {
        InMemoryWidgetPushHub hub = NewHub();
        var tenant = Guid.NewGuid();
        var dashboardA = Guid.NewGuid();
        var dashboardB = Guid.NewGuid();

        var subscribedToA = Channel.CreateUnbounded<WidgetPushMessage>();
        using IDisposable _ = hub.Subscribe(tenant, dashboardA, lastEventId: null, subscribedToA.Writer).Handle;

        await hub.PublishSnapshotAsync(
            tenant, dashboardB, Guid.NewGuid(),
            "Kpi", requiredPermission: null, SamplePayload, EmittedAt, RefreshHint.Realtime,
            cancellationToken: TestContext.Current.CancellationToken);

        subscribedToA.Reader.TryRead(out WidgetPushMessage? _).ShouldBeFalse(); // wrong dashboard
    }

    [Fact]
    public async Task PublishSnapshot_DoesNotLeakAcrossTenants()
    {
        InMemoryWidgetPushHub hub = NewHub();
        var dashboard = Guid.NewGuid();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var subscribedToA = Channel.CreateUnbounded<WidgetPushMessage>();
        using IDisposable _ = hub.Subscribe(tenantA, dashboard, lastEventId: null, subscribedToA.Writer).Handle;

        // Same dashboard id, different tenant — must not leak. Multi-tenant isolation
        // is enforced at the stream-key level, ADR-043 §6.
        await hub.PublishSnapshotAsync(
            tenantB, dashboard, Guid.NewGuid(),
            "Kpi", requiredPermission: null, SamplePayload, EmittedAt, RefreshHint.Realtime,
            cancellationToken: TestContext.Current.CancellationToken);

        subscribedToA.Reader.TryRead(out WidgetPushMessage? _).ShouldBeFalse();
    }

    [Fact]
    public async Task Subscribe_DisposeStopsDelivery()
    {
        InMemoryWidgetPushHub hub = NewHub();
        var tenant = Guid.NewGuid();
        var dashboard = Guid.NewGuid();

        var channel = Channel.CreateUnbounded<WidgetPushMessage>();
        IDisposable handle = hub.Subscribe(tenant, dashboard, lastEventId: null, channel.Writer).Handle;

        handle.Dispose();

        await hub.PublishSnapshotAsync(
            tenant, dashboard, Guid.NewGuid(),
            "Kpi", requiredPermission: null, SamplePayload, EmittedAt, RefreshHint.Realtime,
            cancellationToken: TestContext.Current.CancellationToken);

        channel.Reader.TryRead(out WidgetPushMessage? _).ShouldBeFalse();
    }

    [Fact]
    public async Task PublishUnavailable_CarriesReasonKey()
    {
        InMemoryWidgetPushHub hub = NewHub();
        var tenant = Guid.NewGuid();
        var dashboard = Guid.NewGuid();

        var channel = Channel.CreateUnbounded<WidgetPushMessage>();
        using IDisposable _ = hub.Subscribe(tenant, dashboard, lastEventId: null, channel.Writer).Handle;

        await hub.PublishUnavailableAsync(
            tenant, dashboard, Guid.NewGuid(),
            "Kpi", requiredPermission: null, EmittedAt, RefreshHint.Realtime,
            reasonLocalizationKey: "Widget:Unavailable.MetricNotFound",
            cancellationToken: TestContext.Current.CancellationToken);

        channel.Reader.TryRead(out WidgetPushMessage? message).ShouldBeTrue();
        message!.Envelope.Status.ShouldBe(WidgetSnapshotStatus.Unavailable);
        message.Envelope.Snapshot.ShouldBeNull();
        message.Envelope.ReasonLocalizationKey.ShouldBe("Widget:Unavailable.MetricNotFound");
    }

    [Fact]
    public async Task PublishSnapshot_AssignsMonotonicSequence_PerWidget()
    {
        InMemoryWidgetPushHub hub = NewHub();
        var tenant = Guid.NewGuid();
        var dashboard = Guid.NewGuid();
        var widget = Guid.NewGuid();

        var channel = Channel.CreateUnbounded<WidgetPushMessage>();
        using IDisposable _ = hub.Subscribe(tenant, dashboard, lastEventId: null, channel.Writer).Handle;

        for (int i = 0; i < 3; i++)
        {
            await hub.PublishSnapshotAsync(
                tenant, dashboard, widget,
                "Kpi", requiredPermission: null, SamplePayload, EmittedAt, RefreshHint.Realtime,
            cancellationToken: TestContext.Current.CancellationToken);
        }

        long[] sequences =
        [
            ReadOne(channel).Envelope.Sequence,
            ReadOne(channel).Envelope.Sequence,
            ReadOne(channel).Envelope.Sequence,
        ];

        sequences.ShouldBe([1L, 2L, 3L]);
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
