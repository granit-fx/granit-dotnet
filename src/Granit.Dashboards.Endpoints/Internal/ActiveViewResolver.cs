using System.Security.Cryptography;
using System.Text;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Granit.Dashboards.Widgets;

namespace Granit.Dashboards.Endpoints.Internal;

/// <summary>
/// Resolves the active <see cref="DashboardView"/> requested by a render call,
/// and materialises an ephemeral widget pool when the resolved view is NOT the
/// dashboard's entry view. P2.1 multi-view dispatch.
/// </summary>
/// <remarks>
/// <para>
/// The fallback chain mirrors the frontend's <c>resolveActiveView</c>:
/// requested <c>ViewName</c> → <c>DashboardDefinition.DefaultView</c> → first
/// declared view. Single-view dashboards (no <see cref="IDashboardDefinitionDescriptor.Views"/>)
/// short-circuit to the persisted top-level pool with
/// <c>ActiveViewName: null</c>.
/// </para>
/// <para>
/// "Entry view" — the view whose widgets the importer persisted on the
/// <see cref="Dashboard"/>. v1 assumption: <c>descriptor.DefaultView</c> (or the
/// first view when <c>DefaultView</c> is null) was the entry view at import
/// time. When the active view matches the entry view, <see cref="Dashboard.Widgets"/>
/// is rendered directly so per-instance overrides survive. For non-entry views
/// the renderer falls through to definitions and materialises ephemeral
/// <see cref="WidgetInstance"/>s with deterministic ids derived from
/// <c>(dashboardId, viewName, slug)</c> so the frontend's TanStack cache can
/// partition cleanly across views.
/// </para>
/// </remarks>
internal static class ActiveViewResolver
{
    /// <summary>
    /// Determines the widget pool to render and the active view name to echo on
    /// the response.
    /// </summary>
    public static ResolvedRenderTarget Resolve(
        Dashboard dashboard,
        IDashboardDefinitionDescriptor? descriptor,
        string? requestedViewName)
    {
        ArgumentNullException.ThrowIfNull(dashboard);

        if (descriptor?.Views is not { Count: > 0 } views)
        {
            // Single-view dashboard or no registered source: render the persisted
            // pool. ActiveViewName is null — the frontend treats this as
            // "single-view, no view selector".
            return new ResolvedRenderTarget(dashboard.Widgets, ActiveViewName: null);
        }

        // Resolve the active view: request value → DefaultView → first.
        DashboardView active =
            (requestedViewName is { Length: > 0 }
                ? views.FirstOrDefault(v => string.Equals(v.Name, requestedViewName, StringComparison.Ordinal))
                : null)
            ?? (descriptor.DefaultView is { Length: > 0 }
                ? views.FirstOrDefault(v => string.Equals(v.Name, descriptor.DefaultView, StringComparison.Ordinal))
                : null)
            ?? views[0];

        // Entry view assumption: descriptor.DefaultView (or views[0] when
        // DefaultView is null) is the view whose widgets the importer pinned on
        // the persisted dashboard. v1 accepts drift if DefaultView changed
        // post-import — see ADR-038 deferred drift detection.
        string entryViewName = descriptor.DefaultView is { Length: > 0 } d
            && views.Any(v => string.Equals(v.Name, d, StringComparison.Ordinal))
            ? d
            : views[0].Name;

        bool isEntryView = string.Equals(active.Name, entryViewName, StringComparison.Ordinal);

        IReadOnlyList<WidgetInstance> widgets = isEntryView
            ? dashboard.Widgets
            : Materialise(dashboard.Id, descriptor.Name, active);

        return new ResolvedRenderTarget(widgets, active.Name);
    }

    private static List<WidgetInstance> Materialise(
        Guid dashboardId,
        string definitionName,
        DashboardView view)
    {
        var widgets = new List<WidgetInstance>(view.Widgets.Count);

        foreach (WidgetDefinition definition in view.Widgets)
        {
            WidgetDefinitionToInstanceMapper.Mapping mapping =
                WidgetDefinitionToInstanceMapper.Map(definition);

            // Stable id derived from (dashboardId, viewName, slug) — RFC 9562 v8
            // (custom name-based UUID) using SHA-256 truncated to 16 bytes. Lets
            // the frontend cache per-view on a stable widget id without needing
            // per-view persistence.
            Guid id = DeriveId(dashboardId, view.Name, definition.Slug);

            widgets.Add(WidgetInstance.Create(
                id: id,
                dashboardId: dashboardId,
                widgetType: mapping.WidgetType,
                position: definition.Position,
                width: definition.Size.Width,
                height: definition.Size.Height,
                titleLocalizationKey: $"Widget:{definitionName}.{definition.Slug}",
                configJson: mapping.ConfigJson,
                metricName: mapping.MetricName,
                queryName: mapping.QueryName,
                requiredPermission: definition.RequiredPermission));
        }

        return widgets;
    }

    private static Guid DeriveId(Guid dashboardId, string viewName, string slug)
    {
        // Deterministic per (dashboardId, viewName, slug) so view switches don't
        // generate fresh ids on every render — the frontend's TanStack cache
        // keys per (dashboardId, viewName, widgetId) and stays warm across
        // re-fetches. SHA-256 chosen over SHA-1 (CA5350) — this is purely an
        // identifier-derivation function with no security boundary.
        int seedLength = 16 + Encoding.UTF8.GetByteCount(viewName) + Encoding.UTF8.GetByteCount(slug);
        Span<byte> seed = seedLength <= 256 ? stackalloc byte[seedLength] : new byte[seedLength];
        dashboardId.TryWriteBytes(seed[..16]);
        int offset = 16;
        offset += Encoding.UTF8.GetBytes(viewName, seed[offset..]);
        offset += Encoding.UTF8.GetBytes(slug, seed[offset..]);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(seed[..offset], hash);

        // RFC 9562 §5.8 (UUID version 8 — custom name-based). Set version 8 in
        // the high nibble of byte 6 and the IETF variant bits (10xx) in byte 8.
        Span<byte> guidBytes = stackalloc byte[16];
        hash[..16].CopyTo(guidBytes);
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x80);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return new Guid(guidBytes);
    }
}

/// <summary>
/// Output of <see cref="ActiveViewResolver.Resolve"/> — a widget pool to render
/// plus the active view name to echo on <see cref="Dtos.DashboardRenderResponse.ActiveViewName"/>.
/// </summary>
/// <param name="Widgets">Widgets to render — either persisted or materialised.</param>
/// <param name="ActiveViewName"><c>null</c> for single-view dashboards; the resolved view name otherwise.</param>
internal sealed record ResolvedRenderTarget(
    IReadOnlyList<WidgetInstance> Widgets,
    string? ActiveViewName);
