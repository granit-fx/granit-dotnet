using System.Collections.Concurrent;
using System.Text.Json;
using Granit.Analytics.Metrics;
using Granit.Authorization;
using Granit.Dashboards;
using Granit.Dashboards.Push.Internal;
using Granit.Dashboards.Rendering;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Push.Tests.Internal;

/// <summary>
/// Pins the per-widget permission gate on the SSE push path: envelopes whose
/// <see cref="WidgetPushMessage.RequiredPermission"/> the current user lacks
/// get rewritten to <see cref="WidgetSnapshotStatus.Unavailable"/> with the
/// generic reason key — the producer's specific reason never leaks to a
/// non-entitled subscriber. Mirrors the render-time gating in
/// <c>DashboardRenderer</c>.
/// </summary>
public sealed class WidgetPushPermissionFilterTests
{
    private static readonly JsonElement SamplePayload = JsonSerializer.SerializeToElement(new { value = 42 });
    private static readonly DateTimeOffset EmittedAt = new(2026, 4, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ResolveEffective_NoRequiredPermission_PassesEnvelopeThrough()
    {
        WidgetPushMessage message = NewSnapshotMessage(requiredPermission: null);
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        ConcurrentDictionary<string, bool> cache = new(StringComparer.Ordinal);

        WidgetSnapshotEnvelope effective = await WidgetPushPermissionFilter.ResolveEffectiveAsync(
            message, checker, cache, TestContext.Current.CancellationToken);

        effective.ShouldBeSameAs(message.Envelope);
        await checker.DidNotReceive().IsGrantedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveEffective_PermissionGranted_PassesEnvelopeThrough()
    {
        WidgetPushMessage message = NewSnapshotMessage(requiredPermission: "Invoicing.Invoices.Read");
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync("Invoicing.Invoices.Read", Arg.Any<CancellationToken>()).Returns(true);
        ConcurrentDictionary<string, bool> cache = new(StringComparer.Ordinal);

        WidgetSnapshotEnvelope effective = await WidgetPushPermissionFilter.ResolveEffectiveAsync(
            message, checker, cache, TestContext.Current.CancellationToken);

        effective.ShouldBeSameAs(message.Envelope);
    }

    [Fact]
    public async Task ResolveEffective_PermissionDenied_RewritesToUnavailable()
    {
        WidgetPushMessage message = NewSnapshotMessage(requiredPermission: "Invoicing.Invoices.Read");
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync("Invoicing.Invoices.Read", Arg.Any<CancellationToken>()).Returns(false);
        ConcurrentDictionary<string, bool> cache = new(StringComparer.Ordinal);

        WidgetSnapshotEnvelope effective = await WidgetPushPermissionFilter.ResolveEffectiveAsync(
            message, checker, cache, TestContext.Current.CancellationToken);

        effective.Status.ShouldBe(WidgetSnapshotStatus.Unavailable);
        effective.WidgetType.ShouldBe("Kpi");
        effective.Sequence.ShouldBe(message.Envelope.Sequence);              // sequence preserved
        effective.EmittedAt.ShouldBe(message.Envelope.EmittedAt);            // timing preserved
        effective.RefreshHint.ShouldBe(message.Envelope.RefreshHint);
        effective.Snapshot.ShouldBeNull();                                   // snapshot scrubbed
        effective.ReasonLocalizationKey.ShouldBe("Widget:Unavailable");      // generic reason — no leak
    }

    [Fact]
    public async Task ResolveEffective_PermissionDeniedOnUnavailableEnvelope_RewritesGenericReason()
    {
        // Producer published an Unavailable with a specific reason ("Widget:Unavailable.MetricNotFound").
        // A subscriber lacking the permission must NOT see that reason — the framework rewrites to
        // the generic "Widget:Unavailable" so the producer's diagnostic stays internal.
        var unavailableEnvelope = WidgetSnapshotEnvelope.Unavailable(
            widgetType: "Kpi",
            sequence: 7,
            emittedAt: EmittedAt,
            refreshHint: RefreshHint.Realtime,
            reasonLocalizationKey: "Widget:Unavailable.MetricNotFound");

        WidgetPushMessage message = new(
            TenantId: Guid.NewGuid(),
            DashboardId: Guid.NewGuid(),
            WidgetInstanceId: Guid.NewGuid(),
            RequiredPermission: "Invoicing.Invoices.Read",
            StreamCursor: 1,
            Envelope: unavailableEnvelope);

        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync("Invoicing.Invoices.Read", Arg.Any<CancellationToken>()).Returns(false);
        ConcurrentDictionary<string, bool> cache = new(StringComparer.Ordinal);

        WidgetSnapshotEnvelope effective = await WidgetPushPermissionFilter.ResolveEffectiveAsync(
            message, checker, cache, TestContext.Current.CancellationToken);

        effective.ReasonLocalizationKey.ShouldBe("Widget:Unavailable");      // never leaks "MetricNotFound"
    }

    [Fact]
    public async Task ResolveEffective_CachesPermissionLookup_WithinAStream()
    {
        WidgetPushMessage first = NewSnapshotMessage(requiredPermission: "Invoicing.Invoices.Read");
        WidgetPushMessage second = NewSnapshotMessage(requiredPermission: "Invoicing.Invoices.Read");
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync("Invoicing.Invoices.Read", Arg.Any<CancellationToken>()).Returns(true);
        ConcurrentDictionary<string, bool> cache = new(StringComparer.Ordinal);

        await WidgetPushPermissionFilter.ResolveEffectiveAsync(first, checker, cache, TestContext.Current.CancellationToken);
        await WidgetPushPermissionFilter.ResolveEffectiveAsync(second, checker, cache, TestContext.Current.CancellationToken);

        // Two messages, same permission, single store query — cache hit on the second envelope.
        await checker.Received(1).IsGrantedAsync("Invoicing.Invoices.Read", Arg.Any<CancellationToken>());
    }

    private static WidgetPushMessage NewSnapshotMessage(string? requiredPermission)
    {
        var envelope = WidgetSnapshotEnvelope.ForSnapshot(
            widgetType: "Kpi",
            snapshot: SamplePayload,
            sequence: 1,
            emittedAt: EmittedAt,
            refreshHint: RefreshHint.Realtime);

        return new WidgetPushMessage(
            TenantId: Guid.NewGuid(),
            DashboardId: Guid.NewGuid(),
            WidgetInstanceId: Guid.NewGuid(),
            RequiredPermission: requiredPermission,
            StreamCursor: 1,
            Envelope: envelope);
    }
}
