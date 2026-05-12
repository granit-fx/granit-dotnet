using System.Security.Claims;
using System.Text.Json;
using Granit.Analytics;
using Granit.Analytics.Metrics;
using Granit.Authorization;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Internal;
using Granit.Dashboards.Rendering;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Internal;

/// <summary>
/// Locks the three guarantees ADR-039 §3 concentrates in
/// <see cref="DashboardRenderer"/>: per-widget permission gate (3.a),
/// registry-driven dispatch with Error fallback (3.b), and per-widget
/// exception isolation (3.c). Each test pins one guarantee — failures are
/// regressions that cannot reach production without bumping these tests.
/// </summary>
public sealed class DashboardRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IPermissionChecker _permissionChecker = Substitute.For<IPermissionChecker>();

    public DashboardRendererTests()
    {
        _clock.Now.Returns(Now);
        _permissionChecker.IsGrantedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true); // default: every permission granted
    }

    [Fact]
    public async Task RenderAsync_OrdersWidgetsByPosition_RegardlessOfRendererSpeed()
    {
        Dashboard dashboard = BuildDashboard(
            ("Markdown", Position: 2),
            ("Markdown", Position: 0),
            ("Markdown", Position: 1));

        DashboardRenderer renderer = BuildRenderer(new ImmediateRenderer("Markdown"));

        DashboardRenderResult result = await renderer.RenderAsync(
            dashboard, BuildContext(), TestContext.Current.CancellationToken);

        Guid[] orderedIds = [..
            dashboard.Widgets.OrderBy(w => w.Position).Select(w => w.Id)];

        result.Widgets.Select(rw => rw.WidgetId).ShouldBe(orderedIds);
    }

    [Fact]
    public async Task RenderAsync_PermissionDenied_ReturnsUnavailable_BeforeRendererRuns()
    {
        Dashboard dashboard = BuildDashboard(
            ("Markdown", Position: 0, RequiredPermission: "Module.Resource.Read"));

        _permissionChecker.IsGrantedAsync("Module.Resource.Read", Arg.Any<CancellationToken>())
            .Returns(false);

        ImmediateRenderer markdown = new("Markdown");
        DashboardRenderer renderer = BuildRenderer(markdown);

        DashboardRenderResult result = await renderer.RenderAsync(
            dashboard, BuildContext(), TestContext.Current.CancellationToken);

        markdown.CallCount.ShouldBe(0); // gate must fire BEFORE the renderer
        result.Widgets[0].Envelope.Status.ShouldBe(WidgetSnapshotStatus.Unavailable);
        result.Widgets[0].Envelope.WidgetType.ShouldBe("Markdown");
    }

    [Fact]
    public async Task RenderAsync_NoRequiredPermission_DoesNotCallChecker()
    {
        // Widgets with RequiredPermission == null inherit dashboard-level access —
        // the renderer must not call IPermissionChecker for them (avoids spurious
        // checks on framework-shipped read-everywhere widgets like Markdown).
        Dashboard dashboard = BuildDashboard(("Markdown", Position: 0));

        DashboardRenderer renderer = BuildRenderer(new ImmediateRenderer("Markdown"));

        await renderer.RenderAsync(dashboard, BuildContext(), TestContext.Current.CancellationToken);

        await _permissionChecker.DidNotReceive()
            .IsGrantedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenderAsync_UnknownWidgetType_ReturnsErrorEnvelope()
    {
        Dashboard dashboard = BuildDashboard(("PhantomKind", Position: 0));

        // No renderer registered for "PhantomKind" — must surface Error, not 500.
        DashboardRenderer renderer = BuildRenderer();

        DashboardRenderResult result = await renderer.RenderAsync(
            dashboard, BuildContext(), TestContext.Current.CancellationToken);

        result.Widgets[0].Envelope.Status.ShouldBe(WidgetSnapshotStatus.Error);
        result.Widgets[0].Envelope.WidgetType.ShouldBe("PhantomKind");
        result.Widgets[0].Envelope.ReasonLocalizationKey.ShouldBe("Widget:Error.UnknownWidgetType");
    }

    [Fact]
    public async Task RenderAsync_RendererThrows_ReturnsErrorEnvelope_OtherWidgetsContinue()
    {
        Dashboard dashboard = BuildDashboard(
            ("Markdown", Position: 0),
            ("Boom", Position: 1),
            ("Markdown", Position: 2));

        DashboardRenderer renderer = BuildRenderer(
            new ImmediateRenderer("Markdown"),
            new ThrowingRenderer("Boom"));

        DashboardRenderResult result = await renderer.RenderAsync(
            dashboard, BuildContext(), TestContext.Current.CancellationToken);

        result.Widgets.Count.ShouldBe(3);
        result.Widgets[0].Envelope.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        result.Widgets[1].Envelope.Status.ShouldBe(WidgetSnapshotStatus.Error);
        result.Widgets[1].Envelope.WidgetType.ShouldBe("Boom");
        result.Widgets[1].Envelope.ReasonLocalizationKey.ShouldBe("Widget:Error");
        result.Widgets[2].Envelope.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
    }

    [Fact]
    public async Task RenderAsync_OperationCanceled_PropagatesAsCancellation()
    {
        // Cancellation must propagate (per ADR-039 §3.c) — wrapping it in Error
        // would mask the caller's intent.
        Dashboard dashboard = BuildDashboard(("Markdown", Position: 0));

        DashboardRenderer renderer = BuildRenderer(new CancelingRenderer("Markdown"));

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await renderer.RenderAsync(dashboard, BuildContext(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RenderAsync_EmitsBundleMetadata_FromContextAndClock()
    {
        Dashboard dashboard = BuildDashboard(("Markdown", Position: 0));
        DashboardRenderer renderer = BuildRenderer(new ImmediateRenderer("Markdown"));

        ResolvedPeriod period = new(Now.AddDays(-7), Now);

        DashboardRenderResult result = await renderer.RenderAsync(
            dashboard, BuildContext(period), TestContext.Current.CancellationToken);

        result.DashboardId.ShouldBe(dashboard.Id);
        result.Period.ShouldBe(period);
        result.RenderedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task RenderAsync_EmptyDashboard_ReturnsEmptyWidgetList()
    {
        var dashboard = Dashboard.Create(
            id: Guid.NewGuid(),
            name: "Empty",
            category: DashboardCategory.Finance,
            sourceDefinitionName: null,
            sourceDefinitionVersion: null,
            isSystem: false,
            layoutColumns: 12,
            layoutRowHeight: 60);

        DashboardRenderer renderer = BuildRenderer();

        DashboardRenderResult result = await renderer.RenderAsync(
            dashboard, BuildContext(), TestContext.Current.CancellationToken);

        result.Widgets.ShouldBeEmpty();
    }

    private DashboardRenderer BuildRenderer(params IWidgetInstanceRenderer[] renderers) =>
        new(renderers, _permissionChecker, _clock, NullLogger<DashboardRenderer>.Instance);

    private static WidgetRenderContext BuildContext(ResolvedPeriod? period = null) =>
        new(
            TenantId: null,
            User: new ClaimsPrincipal(new ClaimsIdentity()),
            Period: period,
            Locale: "en",
            DashboardFilters: new Dictionary<string, string>(),
            ResolvedEntityAliases: new Dictionary<string, EntityAliasBinding>());

    private static Dashboard BuildDashboard(params (string WidgetType, int Position, string? RequiredPermission)[] widgets)
    {
        var dashboard = Dashboard.Create(
            id: Guid.NewGuid(),
            name: "TestDashboard",
            category: DashboardCategory.Finance,
            sourceDefinitionName: null,
            sourceDefinitionVersion: null,
            isSystem: false,
            layoutColumns: 12,
            layoutRowHeight: 60);

        foreach ((string widgetType, int position, string? requiredPermission) in widgets)
        {
            dashboard.AddWidget(
                widgetId: Guid.NewGuid(),
                widgetType: widgetType,
                position: position,
                width: 1,
                height: 1,
                titleLocalizationKey: "Widget:Test",
                configJson: "{}",
                requiredPermission: requiredPermission);
        }

        return dashboard;
    }

    private static Dashboard BuildDashboard(params (string WidgetType, int Position)[] widgets) =>
        BuildDashboard([.. widgets.Select(w => (w.WidgetType, w.Position, (string?)null))]);

    private sealed class ImmediateRenderer(string widgetType) : IWidgetInstanceRenderer
    {
        public int CallCount { get; private set; }
        public string WidgetType { get; } = widgetType;

        public Task<WidgetSnapshotEnvelope> RenderAsync(
            WidgetInstance widget, WidgetRenderContext context, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(WidgetSnapshotEnvelope.ForSnapshot(
                widgetType: WidgetType,
                snapshot: JsonSerializer.SerializeToElement(new { ok = true }),
                sequence: 1,
                emittedAt: DateTimeOffset.UtcNow,
                refreshHint: RefreshHint.Static));
        }
    }

    private sealed class ThrowingRenderer(string widgetType) : IWidgetInstanceRenderer
    {
        public string WidgetType { get; } = widgetType;

        public Task<WidgetSnapshotEnvelope> RenderAsync(
            WidgetInstance widget, WidgetRenderContext context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("simulated renderer failure");
    }

    private sealed class CancelingRenderer(string widgetType) : IWidgetInstanceRenderer
    {
        public string WidgetType { get; } = widgetType;

        public Task<WidgetSnapshotEnvelope> RenderAsync(
            WidgetInstance widget, WidgetRenderContext context, CancellationToken cancellationToken) =>
            throw new OperationCanceledException();
    }
}
