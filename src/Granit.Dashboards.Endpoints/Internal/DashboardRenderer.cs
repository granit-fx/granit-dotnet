using Granit.Analytics.Metrics;
using Granit.Authorization;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Dashboards.Endpoints.Internal;

/// <summary>
/// Default <see cref="IDashboardRenderer"/> — keys registered
/// <see cref="IWidgetInstanceRenderer"/> implementations by
/// <see cref="IWidgetInstanceRenderer.WidgetType"/> at startup, then dispatches
/// each persisted widget through the matching renderer with a uniform
/// permission gate (3.a) and per-widget error isolation (3.c) per ADR-039 §3.
/// </summary>
internal sealed partial class DashboardRenderer(
    IEnumerable<IWidgetInstanceRenderer> renderers,
    IPermissionChecker permissionChecker,
    IClock clock,
    ILogger<DashboardRenderer> logger) : IDashboardRenderer
{
    private readonly Dictionary<string, IWidgetInstanceRenderer> _byType =
        renderers.ToDictionary(r => r.WidgetType, StringComparer.Ordinal);

    private readonly IPermissionChecker _permissionChecker = permissionChecker;
    private readonly IClock _clock = clock;
    private readonly ILogger<DashboardRenderer> _logger = logger;

    public Task<DashboardRenderResult> RenderAsync(
        Dashboard dashboard,
        WidgetRenderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dashboard);
        return RenderAsync(dashboard.Id, dashboard.Widgets, context, cancellationToken);
    }

    public async Task<DashboardRenderResult> RenderAsync(
        Guid dashboardId,
        IReadOnlyList<WidgetInstance> widgets,
        WidgetRenderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(widgets);
        ArgumentNullException.ThrowIfNull(context);

        List<RenderedWidget> results = new(widgets.Count);

        // Project to a stable List in Position order BEFORE iterating — keeps the
        // outbound order deterministic regardless of which renderer is faster.
        WidgetInstance[] ordered = [.. widgets.OrderBy(w => w.Position)];

        foreach (WidgetInstance widget in ordered)
        {
            DateTimeOffset emittedAt = _clock.Now;

            // 3.a — permission gate. Drops to Unavailable BEFORE the typed renderer
            //       runs, so a widget the user cannot read never hits the metric / query.
            if (widget.RequiredPermission is { } perm
                && !await _permissionChecker.IsGrantedAsync(perm, cancellationToken).ConfigureAwait(false))
            {
                results.Add(new RenderedWidget(widget.Id, WidgetSnapshotEnvelope.Unavailable(
                    widgetType: widget.WidgetType,
                    sequence: 1,
                    emittedAt: emittedAt,
                    refreshHint: RefreshHint.Static)));
                continue;
            }

            // 3.b — registered renderer? An unknown WidgetType (module unloaded,
            //       version mismatch) becomes Error, not a 500 — one widget cannot
            //       break a whole grid.
            if (!_byType.TryGetValue(widget.WidgetType, out IWidgetInstanceRenderer? renderer))
            {
                LogUnknownWidgetType(widget.Id, widget.WidgetType);
                results.Add(new RenderedWidget(widget.Id, WidgetSnapshotEnvelope.Error(
                    widgetType: widget.WidgetType,
                    sequence: 1,
                    emittedAt: emittedAt,
                    refreshHint: RefreshHint.Static,
                    reasonLocalizationKey: "Widget:Error.UnknownWidgetType")));
                continue;
            }

            // 3.c — typed dispatch. Caller cancellation propagates; any other
            //       exception is captured server-side and returned as Error.
            try
            {
                WidgetSnapshotEnvelope envelope = await renderer
                    .RenderAsync(widget, context, cancellationToken)
                    .ConfigureAwait(false);
                results.Add(new RenderedWidget(widget.Id, envelope));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Per-widget error isolation REQUIRES a generic catch — see ADR-039 §3.c.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogRendererThrew(ex, widget.Id, widget.WidgetType);
                results.Add(new RenderedWidget(widget.Id, WidgetSnapshotEnvelope.Error(
                    widgetType: widget.WidgetType,
                    sequence: 1,
                    emittedAt: emittedAt,
                    refreshHint: RefreshHint.Static)));
            }
        }

        return new DashboardRenderResult(
            DashboardId: dashboardId,
            RenderedAt: _clock.Now,
            Period: context.Period,
            Widgets: results);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Widget {WidgetId} references unknown WidgetType '{WidgetType}' — surfacing Error envelope. Module unloaded or version mismatch?")]
    private partial void LogUnknownWidgetType(Guid widgetId, string widgetType);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "Renderer for widget {WidgetId} ('{WidgetType}') threw — surfacing Error envelope. Per-widget isolation: other widgets continue.")]
    private partial void LogRendererThrew(Exception exception, Guid widgetId, string widgetType);
}
