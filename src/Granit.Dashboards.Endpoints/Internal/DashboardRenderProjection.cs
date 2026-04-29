using Granit.Analytics;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.Rendering;

namespace Granit.Dashboards.Endpoints.Internal;

/// <summary>
/// Translates the rendering-pipeline <see cref="DashboardRenderResult"/> into
/// the HTTP-shape <see cref="DashboardRenderResponse"/>. Lives next to the
/// other projection helpers so the endpoint handler stays reflection-free and
/// the wire shape can be unit-tested without an HTTP harness.
/// </summary>
internal static class DashboardRenderProjection
{
    public static DashboardRenderResponse ToResponse(DashboardRenderResult result, string? periodToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        DashboardRenderPeriodResponse? period = result.Period is { } p
            ? new DashboardRenderPeriodResponse(p.From, p.To, periodToken)
            : null;

        DashboardRenderedWidgetResponse[] widgets = [.. result.Widgets.Select(rw =>
            new DashboardRenderedWidgetResponse(
                Id: rw.WidgetId,
                WidgetType: rw.Envelope.WidgetType,
                Status: rw.Envelope.Status,
                Sequence: rw.Envelope.Sequence,
                EmittedAt: rw.Envelope.EmittedAt,
                RefreshHint: rw.Envelope.RefreshHint,
                Snapshot: rw.Envelope.Snapshot,
                ReasonLocalizationKey: rw.Envelope.ReasonLocalizationKey))];

        return new DashboardRenderResponse(
            DashboardId: result.DashboardId,
            RenderedAt: result.RenderedAt,
            Period: period,
            Widgets: widgets);
    }

    public static ResolvedPeriod? TryBuildResolvedPeriod(DashboardRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.PeriodFrom is { } from && request.PeriodTo is { } to)
        {
            return new ResolvedPeriod(from, to);
        }

        return null;
    }
}
