using Granit.Dashboards.Domain;

namespace Granit.Dashboards.Rendering;

/// <summary>
/// Non-generic dispatch adapter for one widget kind — sits between the per-kind
/// typed <c>IWidgetSource&lt;TSnapshot&gt;</c> (in <c>Granit.Analytics</c>) and the
/// dashboard render endpoint that bundles heterogeneous widgets into one
/// response.
/// </summary>
/// <remarks>
/// <para>
/// Each widget kind ships exactly one implementation registered in DI; the
/// dashboard renderer resolves the matching one by <see cref="WidgetType"/>
/// (case-sensitive string compare against
/// <c>WidgetInstance.WidgetType</c> / <c>WidgetDefinition</c>'s
/// <c>[JsonDerivedType]</c> tag — same discriminator surface).
/// </para>
/// <para>
/// Implementations are <i>typed-source adapters</i>: build the typed source from
/// the persisted <c>WidgetInstance</c> + the <see cref="WidgetRenderContext"/>,
/// call <c>RenderAsync</c>, then materialise the typed snapshot into a
/// <see cref="System.Text.Json.JsonElement"/> via
/// <c>JsonSerializer.SerializeToElement(payload.Snapshot, options)</c> — see
/// ADR-039 §2.1 for why we converge on <c>JsonElement</c> at the boundary
/// instead of <c>object?</c>.
/// </para>
/// <para>
/// Permission filtering and per-widget error isolation are <b>not</b> the
/// renderer's responsibility — the dashboard renderer applies them uniformly
/// before invoking <see cref="RenderAsync"/>. Implementations may assume the
/// caller has already verified the user can read the underlying metric / query.
/// </para>
/// </remarks>
public interface IWidgetInstanceRenderer
{
    /// <summary>
    /// The widget-type discriminator this renderer handles. Matches
    /// <c>WidgetInstance.WidgetType</c> exactly (case-sensitive, ordinal) and
    /// <c>WidgetDefinition</c>'s <c>[JsonDerivedType]</c> <c>"type"</c> tag.
    /// </summary>
    string WidgetType { get; }

    /// <summary>
    /// Renders the widget against the supplied context, returning a
    /// <see cref="WidgetSnapshotEnvelope"/> the dashboard renderer adds to the
    /// bundle response.
    /// </summary>
    /// <param name="widget">The persisted widget instance to render. The renderer reads <c>WidgetType</c>, <c>ConfigJson</c>, <c>MetricName</c> / <c>QueryName</c> as relevant to its kind. Width / height / position are layout — surface them through the snapshot only when the kind needs them client-side.</param>
    /// <param name="context">Per-render context — see <see cref="WidgetRenderContext"/>.</param>
    /// <param name="cancellationToken">Cancellation token; renderers must honour it on every async hop they own.</param>
    Task<WidgetSnapshotEnvelope> RenderAsync(
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken);
}
