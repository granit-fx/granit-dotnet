using System.Text.Json;
using Granit.Analytics;
using Granit.Analytics.Metrics;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.Endpoints.Internal;
using Granit.Dashboards.Rendering;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Internal;

/// <summary>
/// Pins the wire shape locked by ADR-039 §6 — the JSON the endpoint emits
/// must travel through this projection unchanged from the typed envelopes the
/// renderer pipeline produces. Frontend types in
/// <c>granit-front/@granit/analytics</c> consume these fields directly; a
/// silent rename here ripples to TypeScript.
/// </summary>
public sealed class DashboardRenderProjectionTests
{
    private static readonly DateTimeOffset RenderedAt = new(2026, 4, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToResponse_MapsEnvelopeFieldsOntoFlattenedWidgetRecords()
    {
        var widgetId = Guid.NewGuid();
        JsonElement payload = JsonSerializer.SerializeToElement(new { value = 42 });

        DashboardRenderResult result = new(
            DashboardId: Guid.NewGuid(),
            RenderedAt: RenderedAt,
            Period: new ResolvedPeriod(RenderedAt.AddDays(-7), RenderedAt),
            Widgets:
            [
                new RenderedWidget(widgetId, WidgetSnapshotEnvelope.ForSnapshot(
                    widgetType: "Kpi",
                    snapshot: payload,
                    sequence: 1,
                    emittedAt: RenderedAt,
                    refreshHint: RefreshHint.Dynamic)),
            ]);

        DashboardRenderResponse response = DashboardRenderProjection.ToResponse(result, periodToken: "mtd");

        response.DashboardId.ShouldBe(result.DashboardId);
        response.RenderedAt.ShouldBe(RenderedAt);
        response.Period.ShouldNotBeNull();
        response.Period!.From.ShouldBe(RenderedAt.AddDays(-7));
        response.Period.To.ShouldBe(RenderedAt);
        response.Period.Token.ShouldBe("mtd");

        response.Widgets.Count.ShouldBe(1);
        DashboardRenderedWidgetResponse w = response.Widgets[0];
        w.Id.ShouldBe(widgetId);
        w.WidgetType.ShouldBe("Kpi");
        w.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        w.Sequence.ShouldBe(1);
        w.RefreshHint.ShouldBe(RefreshHint.Dynamic);
        w.Snapshot.ShouldNotBeNull();
        w.Snapshot!.Value.GetProperty("value").GetInt32().ShouldBe(42);
        w.ReasonLocalizationKey.ShouldBeNull();
    }

    [Fact]
    public void ToResponse_NullPeriod_OmitsPeriodEnvelopeField()
    {
        DashboardRenderResult result = new(
            DashboardId: Guid.NewGuid(),
            RenderedAt: RenderedAt,
            Period: null,
            Widgets: []);

        DashboardRenderResponse response = DashboardRenderProjection.ToResponse(result, periodToken: null);

        response.Period.ShouldBeNull();
    }

    [Fact]
    public void ToResponse_UnavailableEnvelope_PreservesReasonKey()
    {
        DashboardRenderResult result = new(
            DashboardId: Guid.NewGuid(),
            RenderedAt: RenderedAt,
            Period: null,
            Widgets:
            [
                new RenderedWidget(Guid.NewGuid(), WidgetSnapshotEnvelope.Unavailable(
                    widgetType: "Kpi",
                    sequence: 1,
                    emittedAt: RenderedAt,
                    refreshHint: RefreshHint.Static,
                    reasonLocalizationKey: "Widget:Unavailable.MetricNotFound")),
            ]);

        DashboardRenderResponse response = DashboardRenderProjection.ToResponse(result, periodToken: null);

        DashboardRenderedWidgetResponse w = response.Widgets[0];
        w.Status.ShouldBe(WidgetSnapshotStatus.Unavailable);
        w.Snapshot.ShouldBeNull();
        w.ReasonLocalizationKey.ShouldBe("Widget:Unavailable.MetricNotFound");
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("2026-04-01T00:00:00Z", null, null)]                 // single bound = no period
    [InlineData(null, "2026-05-01T00:00:00Z", null)]                 // single bound = no period
    [InlineData("2026-04-01T00:00:00Z", "2026-05-01T00:00:00Z", "mtd")]
    public void TryBuildResolvedPeriod_RequiresBothBounds(string? from, string? to, string? expectedToken)
    {
        DashboardRenderRequest request = new(
            PeriodFrom: from is null ? null : DateTimeOffset.Parse(from, System.Globalization.CultureInfo.InvariantCulture),
            PeriodTo: to is null ? null : DateTimeOffset.Parse(to, System.Globalization.CultureInfo.InvariantCulture));

        ResolvedPeriod? period = DashboardRenderProjection.TryBuildResolvedPeriod(request);

        if (from is null || to is null)
        {
            period.ShouldBeNull();
        }
        else
        {
            period.ShouldNotBeNull();
            // Asserting expectedToken non-null for completeness — the token isn't
            // part of ResolvedPeriod itself, so we just assert resolution succeeded.
            expectedToken.ShouldNotBeNull();
        }
    }
}
