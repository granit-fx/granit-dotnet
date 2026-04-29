using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Granit.Analytics.Endpoints.Dtos.Widgets;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Granit.Dashboards.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Shared rendering pipeline for the per-widget endpoints
/// (<c>POST /widgets/{kind}/render</c>). Materialises an ephemeral
/// <see cref="WidgetInstance"/> from the typed <see cref="WidgetDefinition"/>
/// supplied in the request body, dispatches it through the existing
/// <see cref="IDashboardRenderer"/> machinery (reusing the Guid-based overload
/// shipped in P2.1), and projects the resulting envelope onto the same
/// <see cref="DashboardRenderedWidgetResponse"/> shape the bundle path emits.
/// </summary>
/// <remarks>
/// <para>
/// Design intent — symmetry with the bundle path. The frontend wraps the same
/// snapshot widgets (KpiSnapshotTile / ChartSnapshotWidget / ...) in both the
/// definition path (per-widget single render) and the bundle path
/// (<c>RenderedDashboard</c>). Returning the full <see cref="DashboardRenderedWidgetResponse"/>
/// here — not just the snapshot payload — means a frontend dispatcher does not
/// have to know which path the envelope came from.
/// </para>
/// <para>
/// Ephemeral identifiers. The widget is rendered without a persisted dashboard;
/// the dashboard id is <see cref="Guid.Empty"/> and the widget id is derived
/// deterministically from <see cref="WidgetDefinition.Slug"/> via SHA-256
/// (RFC 9562 v8 — custom name-based UUID). Stable across re-fetches so the
/// frontend's TanStack cache stays warm; never collides with persisted ids
/// because <see cref="Guid.Empty"/> as the dashboard component anchors the
/// hash to the ad-hoc namespace.
/// </para>
/// </remarks>
internal static class WidgetRenderHelper
{
    // GRAPI003 false positive — this is a helper invoked by endpoint handlers
    // that themselves decorate the renderer parameter with [FromServices]; the
    // analyzer can't see through the call. Suppress per-method.
#pragma warning disable GRAPI003
    public static async Task<Ok<DashboardRenderedWidgetResponse>> RenderAsync(
        WidgetDefinition definition,
        WidgetRenderContextRequest? contextRequest,
        IDashboardRenderer renderer,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
#pragma warning restore GRAPI003
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(user);

        // Materialise an ephemeral WidgetInstance from the typed definition. The
        // mapper produces (WidgetType, MetricName?, QueryName?, ConfigJson) — the
        // exact shape the persisted aggregate carries, so the existing
        // IWidgetInstanceRenderer dispatch table works unchanged.
        WidgetDefinitionToInstanceMapper.Mapping mapping =
            WidgetDefinitionToInstanceMapper.Map(definition);

        Guid widgetId = DeriveWidgetId(definition.Slug);
        string titleLocalizationKey = $"Widget:{definition.Slug}";

        var widget = WidgetInstance.Create(
            id: widgetId,
            dashboardId: Guid.Empty,                                            // ad-hoc render — no persisted dashboard
            widgetType: mapping.WidgetType,
            position: definition.Position,
            width: definition.Size.Width,
            height: definition.Size.Height,
            titleLocalizationKey: titleLocalizationKey,
            configJson: mapping.ConfigJson,
            metricName: mapping.MetricName,
            queryName: mapping.QueryName,
            requiredPermission: definition.RequiredPermission);

        ResolvedPeriod? period = (contextRequest?.PeriodFrom is { } from && contextRequest?.PeriodTo is { } to)
            ? new ResolvedPeriod(from, to)
            : null;

        WidgetRenderContext context = new(
            TenantId: null,                                                     // populated upstream via ICurrentTenant when wired into the host
            User: user,
            Period: period,
            Locale: contextRequest?.Locale ?? "en",
            DashboardFilters: contextRequest?.Filters ?? new Dictionary<string, string>(),
            ResolvedEntityAliases: new Dictionary<string, EntityAliasBinding>());

        DashboardRenderResult result = await renderer
            .RenderAsync(Guid.Empty, [widget], context, cancellationToken)
            .ConfigureAwait(false);

        RenderedWidget rendered = result.Widgets[0];

        return TypedResults.Ok(new DashboardRenderedWidgetResponse(
            Id: rendered.WidgetId,
            WidgetType: rendered.Envelope.WidgetType,
            Slug: definition.Slug,
            Position: definition.Position,
            Width: definition.Size.Width,
            Height: definition.Size.Height,
            TitleLocalizationKey: titleLocalizationKey,
            // Actions come straight from the request body — no descriptor look-up
            // needed since the caller passed the whole WidgetDefinition, including
            // its declarative click-handlers.
            Actions: definition.Actions,
            RequiredPermission: definition.RequiredPermission,
            Status: rendered.Envelope.Status,
            Sequence: rendered.Envelope.Sequence,
            EmittedAt: rendered.Envelope.EmittedAt,
            RefreshHint: rendered.Envelope.RefreshHint,
            Snapshot: rendered.Envelope.Snapshot,
            ReasonLocalizationKey: rendered.Envelope.ReasonLocalizationKey));
    }

    private static Guid DeriveWidgetId(string slug)
    {
        // RFC 9562 v8 custom name-based UUID over the slug. Deterministic so
        // refetching the same definition reuses the same widget id and the
        // frontend's TanStack cache stays warm across renders.
        int seedLength = Encoding.UTF8.GetByteCount(slug);
        Span<byte> seed = seedLength <= 256 ? stackalloc byte[seedLength] : new byte[seedLength];
        Encoding.UTF8.GetBytes(slug, seed);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(seed, hash);

        Span<byte> guidBytes = stackalloc byte[16];
        hash[..16].CopyTo(guidBytes);
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x80);                    // version 8
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);                    // IETF variant

        return new Guid(guidBytes);
    }
}
