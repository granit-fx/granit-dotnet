using System.Security.Claims;
using System.Text.Json;
using Granit.Analytics.Dashboards.Widgets;
using Granit.Analytics.Endpoints.Dtos.Widgets;
using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.Rendering;
using Granit.QueryEngine.Filtering;
using Microsoft.AspNetCore.Http.HttpResults;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Internal;

/// <summary>
/// Pins the wire-shape symmetry between the bundle path
/// (<c>POST /dashboards/{id}/render</c>) and the per-widget path
/// (<c>POST /widgets/{kind}/render</c>) — both projects emit the same
/// <see cref="DashboardRenderedWidgetResponse"/> so a frontend dispatcher
/// can wrap snapshot widgets identically across the two paths.
/// </summary>
public sealed class WidgetRenderHelperTests
{
    private static readonly DateTimeOffset RenderedAt = new(2026, 4, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly ClaimsPrincipal AnonymousUser = new(new ClaimsIdentity());

    [Fact]
    public async Task RenderAsync_ProjectsEnvelopeOntoDashboardRenderedWidgetResponse()
    {
        ChartWidgetDefinition definition = new(
            Slug: "unpaid-by-month",
            QueryName: "Granit.Invoicing.InvoiceQuery",
            GroupBy: "IssuedAtMonth",
            Aggregation: AggregateFunction.Sum,
            Field: "AmountRemaining",
            ChartType: ChartType.Bar,
            Position: 3,
            Actions: [new WidgetAction(WidgetActionTrigger.Click, WidgetActionKind.Navigate, "/invoicing")]);

        IDashboardRenderer renderer = Substitute.For<IDashboardRenderer>();
        renderer.RenderAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<Granit.Dashboards.Domain.WidgetInstance>>(),
            Arg.Any<WidgetRenderContext>(),
            Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Guid dashboardId = call.Arg<Guid>();
                IReadOnlyList<Granit.Dashboards.Domain.WidgetInstance> widgets =
                    call.Arg<IReadOnlyList<Granit.Dashboards.Domain.WidgetInstance>>();
                Granit.Dashboards.Domain.WidgetInstance widget = widgets[0];
                return new DashboardRenderResult(
                    dashboardId,
                    RenderedAt,
                    Period: null,
                    Widgets: [
                        new RenderedWidget(widget.Id, WidgetSnapshotEnvelope.ForSnapshot(
                            widgetType: widget.WidgetType,
                            snapshot: JsonSerializer.SerializeToElement(new { categories = new[] { "Jan" }, values = new[] { 42 } }),
                            sequence: 1,
                            emittedAt: RenderedAt,
                            refreshHint: RefreshHint.Dynamic)),
                    ]);
            });

        Ok<DashboardRenderedWidgetResponse> result = await WidgetRenderHelper.RenderAsync(
            definition,
            contextRequest: new WidgetRenderContextRequest(Locale: "fr-CA"),
            renderer,
            AnonymousUser,
            TestContext.Current.CancellationToken);

        DashboardRenderedWidgetResponse response = result.Value!;

        // Structural metadata projected straight from the request body — no
        // descriptor lookup needed since the caller passed the full definition.
        response.Slug.ShouldBe("unpaid-by-month");
        response.WidgetType.ShouldBe("Chart");
        response.Position.ShouldBe(3);
        response.Width.ShouldBe(WidgetSize.StandardChart.Width);
        response.Height.ShouldBe(WidgetSize.StandardChart.Height);
        response.TitleLocalizationKey.ShouldBe("Widget:unpaid-by-month");
        response.RequiredPermission.ShouldBeNull();

        // Actions echo the body's declarative click-handlers — frontend dispatcher
        // sees the same shape as the bundle path emits.
        response.Actions.ShouldNotBeNull();
        response.Actions!.Count.ShouldBe(1);
        response.Actions[0].Trigger.ShouldBe(WidgetActionTrigger.Click);
        response.Actions[0].Kind.ShouldBe(WidgetActionKind.Navigate);
        response.Actions[0].Target.ShouldBe("/invoicing");

        // Renderer envelope flowed through unchanged.
        response.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        response.Sequence.ShouldBe(1);
        response.RefreshHint.ShouldBe(RefreshHint.Dynamic);
        response.Snapshot.ShouldNotBeNull();
        response.ReasonLocalizationKey.ShouldBeNull();
    }

    [Fact]
    public async Task RenderAsync_DerivesStableWidgetIdAcrossInvocations()
    {
        ChartWidgetDefinition definition = new(
            Slug: "deterministic-id",
            QueryName: "Granit.Test.Q",
            GroupBy: "x",
            Aggregation: AggregateFunction.Count,
            Field: null,
            ChartType: ChartType.Bar,
            Position: 0);

        IDashboardRenderer renderer = Substitute.For<IDashboardRenderer>();
        renderer.RenderAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<Granit.Dashboards.Domain.WidgetInstance>>(),
            Arg.Any<WidgetRenderContext>(),
            Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Guid dashboardId = call.Arg<Guid>();
                IReadOnlyList<Granit.Dashboards.Domain.WidgetInstance> widgets =
                    call.Arg<IReadOnlyList<Granit.Dashboards.Domain.WidgetInstance>>();
                Granit.Dashboards.Domain.WidgetInstance widget = widgets[0];
                return new DashboardRenderResult(
                    dashboardId,
                    RenderedAt,
                    null,
                    [new RenderedWidget(widget.Id, WidgetSnapshotEnvelope.ForSnapshot(
                        widget.WidgetType,
                        JsonSerializer.SerializeToElement(new { v = 1 }),
                        1, RenderedAt, RefreshHint.Static))]);
            });

        Ok<DashboardRenderedWidgetResponse> first = await WidgetRenderHelper.RenderAsync(
            definition, null, renderer, AnonymousUser, TestContext.Current.CancellationToken);
        Ok<DashboardRenderedWidgetResponse> second = await WidgetRenderHelper.RenderAsync(
            definition, null, renderer, AnonymousUser, TestContext.Current.CancellationToken);

        // Same slug -> same widget id -> frontend's TanStack cache key stays warm.
        first.Value!.Id.ShouldBe(second.Value!.Id);
    }

    [Fact]
    public async Task RenderAsync_PassesPeriodAndLocaleToRenderer()
    {
        ChartWidgetDefinition definition = new(
            Slug: "period-test",
            QueryName: "Granit.Test.Q",
            GroupBy: "x",
            Aggregation: AggregateFunction.Count,
            Field: null,
            ChartType: ChartType.Bar,
            Position: 0);

        IDashboardRenderer renderer = Substitute.For<IDashboardRenderer>();
        WidgetRenderContext? capturedContext = null;
        renderer.RenderAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<Granit.Dashboards.Domain.WidgetInstance>>(),
            Arg.Do<WidgetRenderContext>(c => capturedContext = c),
            Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Guid dashboardId = call.Arg<Guid>();
                IReadOnlyList<Granit.Dashboards.Domain.WidgetInstance> widgets =
                    call.Arg<IReadOnlyList<Granit.Dashboards.Domain.WidgetInstance>>();
                Granit.Dashboards.Domain.WidgetInstance widget = widgets[0];
                return new DashboardRenderResult(
                    dashboardId,
                    RenderedAt,
                    null,
                    [new RenderedWidget(widget.Id, WidgetSnapshotEnvelope.ForSnapshot(
                        widget.WidgetType,
                        JsonSerializer.SerializeToElement(new { }),
                        1, RenderedAt, RefreshHint.Static))]);
            });

        DateTimeOffset from = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset to = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

        await WidgetRenderHelper.RenderAsync(
            definition,
            new WidgetRenderContextRequest(PeriodFrom: from, PeriodTo: to, Locale: "fr"),
            renderer,
            AnonymousUser,
            TestContext.Current.CancellationToken);

        capturedContext.ShouldNotBeNull();
        capturedContext!.Period.ShouldNotBeNull();
        capturedContext.Period!.Value.From.ShouldBe(from);
        capturedContext.Period.Value.To.ShouldBe(to);
        capturedContext.Locale.ShouldBe("fr");
    }
}
