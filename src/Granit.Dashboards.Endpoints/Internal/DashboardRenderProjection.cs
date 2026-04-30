using System.Text.RegularExpressions;
using Granit.Analytics;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Endpoints.Dtos;
using Granit.Dashboards.Rendering;

namespace Granit.Dashboards.Endpoints.Internal;

/// <summary>
/// Translates the rendering-pipeline <see cref="DashboardRenderResult"/> into
/// the HTTP-shape <see cref="DashboardRenderResponse"/>. Lives next to the
/// other projection helpers so the endpoint handler stays reflection-free and
/// the wire shape can be unit-tested without an HTTP harness.
/// </summary>
internal static partial class DashboardRenderProjection
{
    /// <summary>
    /// Semver shape accepted by drift detection: <c>MAJOR.MINOR.PATCH</c> with an
    /// optional pre-release (<c>-alpha.1</c>) or build-metadata (<c>+build.7</c>)
    /// suffix. Strings outside this shape collapse to
    /// <see cref="DashboardDriftStatus.Unknown"/> (cautious default).
    /// </summary>
    [GeneratedRegex(@"^(\d+)\.(\d+)\.(\d+)([-+].*)?$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex SemverRegex();

    /// <summary>
    /// Composes the wire response from the renderer's pipeline result, the
    /// persisted <see cref="Dashboard"/> aggregate (source of structural metadata
    /// — <c>Position</c>, <c>Width</c>, <c>Height</c>, <c>TitleLocalizationKey</c>,
    /// <c>RequiredPermission</c>), and an <see cref="IDashboardDefinitionRegistry"/>
    /// look-up that surfaces the declarative <see cref="WidgetAction"/> list from
    /// the source <see cref="WidgetDefinition"/>.
    /// </summary>
    /// <remarks>
    /// Actions are looked up by <see cref="Dashboard.SourceDefinitionName"/> +
    /// per-widget slug — when the source definition is no longer registered (or
    /// the dashboard was custom-built without a source definition),
    /// <see cref="DashboardRenderedWidgetResponse.Actions"/> is <see langword="null"/>.
    /// Definition / persisted-aggregate version drift is intentionally accepted
    /// for v1: the look-up uses the latest registered version even when
    /// <see cref="Dashboard.SourceDefinitionVersion"/> differs. See ADR-038
    /// "drift detection" deferred work.
    /// </remarks>
    public static DashboardRenderResponse ToResponse(
        DashboardRenderResult result,
        Dashboard dashboard,
        ResolvedRenderTarget target,
        IDashboardDefinitionRegistry definitionRegistry,
        string? periodToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(dashboard);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(definitionRegistry);

        DashboardRenderPeriodResponse? period = result.Period is { } p
            ? new DashboardRenderPeriodResponse(p.From, p.To, periodToken)
            : null;

        Dictionary<string, IReadOnlyList<WidgetAction>?> actionsBySlug =
            ResolveActionsBySlug(dashboard, definitionRegistry);

        // The widget pool actually rendered is the resolved render target's pool —
        // either persisted (entry view / single-view) or materialised (non-entry).
        // The structural metadata (Position, Width, Height, ...) on each
        // RenderedWidget therefore comes from `target.Widgets`, not from
        // `dashboard.Widgets` (which only carries the entry view).
        var instanceById = target.Widgets.ToDictionary(w => w.Id);

        DashboardRenderedWidgetResponse[] widgets = [.. result.Widgets.Select(rw =>
        {
            WidgetInstance instance = instanceById[rw.WidgetId];
            string slug = ExtractSlug(instance.TitleLocalizationKey, dashboard.SourceDefinitionName);
            actionsBySlug.TryGetValue(slug, out IReadOnlyList<WidgetAction>? actions);

            return new DashboardRenderedWidgetResponse(
                Id: rw.WidgetId,
                WidgetType: rw.Envelope.WidgetType,
                Slug: slug,
                Position: instance.Position,
                Width: instance.Width,
                Height: instance.Height,
                TitleLocalizationKey: instance.TitleLocalizationKey,
                Actions: actions,
                RequiredPermission: instance.RequiredPermission,
                Status: rw.Envelope.Status,
                Sequence: rw.Envelope.Sequence,
                EmittedAt: rw.Envelope.EmittedAt,
                RefreshHint: rw.Envelope.RefreshHint,
                Snapshot: rw.Envelope.Snapshot,
                ReasonLocalizationKey: rw.Envelope.ReasonLocalizationKey);
        })];

        (DashboardDriftStatus drift, string? registeredVersion) =
            ResolveDriftStatus(dashboard, definitionRegistry);

        return new DashboardRenderResponse(
            DashboardId: result.DashboardId,
            RenderedAt: result.RenderedAt,
            Period: period,
            ActiveViewName: target.ActiveViewName,
            DriftStatus: drift,
            SourceDefinitionVersion: dashboard.SourceDefinitionVersion,
            RegisteredVersion: registeredVersion,
            Widgets: widgets);
    }

    /// <summary>
    /// ADR-038 §3 drift detection. Compares the persisted dashboard's
    /// <see cref="Dashboard.SourceDefinitionVersion"/> against the currently-registered
    /// descriptor's <see cref="IDashboardDefinitionDescriptor.Version"/> using a
    /// semver-aware ordering (MAJOR.MINOR.PATCH numeric tuple, suffix string-equal).
    /// </summary>
    /// <remarks>
    /// Mirrors the frontend's <c>compareVersions</c> helper in
    /// <c>granit-front/@granit/dashboards</c>: numeric tuple drives
    /// <see cref="DashboardDriftStatus.Behind"/> / <see cref="DashboardDriftStatus.Ahead"/>;
    /// matching tuples with identical suffixes report
    /// <see cref="DashboardDriftStatus.Aligned"/>; matching tuples with mismatched
    /// suffixes (or strings outside the semver shape) report
    /// <see cref="DashboardDriftStatus.Unknown"/>. Keeping both ends in lock-step
    /// avoids a UI banner that disagrees with the wire response on what "drift"
    /// means.
    /// </remarks>
    private static (DashboardDriftStatus Status, string? RegisteredVersion) ResolveDriftStatus(
        Dashboard dashboard,
        IDashboardDefinitionRegistry registry)
    {
        if (dashboard.SourceDefinitionName is not { Length: > 0 } sourceName)
        {
            return (DashboardDriftStatus.NotApplicable, null);
        }

        IDashboardDefinitionDescriptor? descriptor = registry.Find(sourceName);
        if (descriptor is null)
        {
            return (DashboardDriftStatus.SourceUnregistered, null);
        }

        DashboardDriftStatus status = CompareSemver(dashboard.SourceDefinitionVersion, descriptor.Version);
        return (status, descriptor.Version);
    }

    /// <summary>
    /// Pure semver compare; returns one of <see cref="DashboardDriftStatus.Aligned"/>
    /// / <see cref="DashboardDriftStatus.Behind"/> / <see cref="DashboardDriftStatus.Ahead"/>
    /// / <see cref="DashboardDriftStatus.Unknown"/>. Marked <see langword="internal"/>
    /// so the projection unit tests can pin every branch of the matrix without
    /// reaching for a full <c>Dashboard</c> aggregate.
    /// </summary>
    internal static DashboardDriftStatus CompareSemver(string? persisted, string? registered)
    {
        if (persisted is not { Length: > 0 } || registered is not { Length: > 0 })
        {
            return DashboardDriftStatus.Unknown;
        }

        Match persistedMatch = SemverRegex().Match(persisted);
        Match registeredMatch = SemverRegex().Match(registered);
        if (!persistedMatch.Success || !registeredMatch.Success)
        {
            return DashboardDriftStatus.Unknown;
        }

        int persistedMajor = int.Parse(persistedMatch.Groups[1].ValueSpan, System.Globalization.CultureInfo.InvariantCulture);
        int persistedMinor = int.Parse(persistedMatch.Groups[2].ValueSpan, System.Globalization.CultureInfo.InvariantCulture);
        int persistedPatch = int.Parse(persistedMatch.Groups[3].ValueSpan, System.Globalization.CultureInfo.InvariantCulture);
        string persistedSuffix = persistedMatch.Groups[4].Value;

        int registeredMajor = int.Parse(registeredMatch.Groups[1].ValueSpan, System.Globalization.CultureInfo.InvariantCulture);
        int registeredMinor = int.Parse(registeredMatch.Groups[2].ValueSpan, System.Globalization.CultureInfo.InvariantCulture);
        int registeredPatch = int.Parse(registeredMatch.Groups[3].ValueSpan, System.Globalization.CultureInfo.InvariantCulture);
        string registeredSuffix = registeredMatch.Groups[4].Value;

        int majorCmp = persistedMajor.CompareTo(registeredMajor);
        if (majorCmp != 0)
        {
            return majorCmp < 0 ? DashboardDriftStatus.Behind : DashboardDriftStatus.Ahead;
        }

        int minorCmp = persistedMinor.CompareTo(registeredMinor);
        if (minorCmp != 0)
        {
            return minorCmp < 0 ? DashboardDriftStatus.Behind : DashboardDriftStatus.Ahead;
        }

        int patchCmp = persistedPatch.CompareTo(registeredPatch);
        if (patchCmp != 0)
        {
            return patchCmp < 0 ? DashboardDriftStatus.Behind : DashboardDriftStatus.Ahead;
        }

        // Numeric tuple matches — suffix must agree byte-for-byte to claim alignment.
        // Pre-release ordering per semver §11 is intentionally not implemented: cautious
        // default of Unknown lets the frontend show both raw strings instead of guessing
        // whether "1.0.0-rc.1" precedes or follows "1.0.0-beta.2".
        return string.Equals(persistedSuffix, registeredSuffix, StringComparison.Ordinal)
            ? DashboardDriftStatus.Aligned
            : DashboardDriftStatus.Unknown;
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

    private static Dictionary<string, IReadOnlyList<WidgetAction>?> ResolveActionsBySlug(
        Dashboard dashboard,
        IDashboardDefinitionRegistry registry)
    {
        Dictionary<string, IReadOnlyList<WidgetAction>?> map = new(StringComparer.Ordinal);

        if (dashboard.SourceDefinitionName is not { } sourceName)
        {
            return map;
        }

        IDashboardDefinitionDescriptor? descriptor = registry.Find(sourceName);
        if (descriptor is null)
        {
            return map;
        }

        // A dashboard's persisted widgets came from one of: (a) the descriptor's
        // top-level Widgets pool, or (b) one named DashboardView's pool. Walk
        // every reachable pool and project Slug -> Actions; later pools win on
        // duplicate slugs (definition authors shouldn't reuse slugs across views,
        // but this never throws if they do).
        AddPool(map, descriptor.Widgets);
        if (descriptor.Views is { } views)
        {
            foreach (DashboardView view in views)
            {
                AddPool(map, view.Widgets);
            }
        }

        return map;

        static void AddPool(
            Dictionary<string, IReadOnlyList<WidgetAction>?> map,
            IReadOnlyList<WidgetDefinition> pool)
        {
            foreach (WidgetDefinition widget in pool)
            {
                map[widget.Slug] = widget.Actions;
            }
        }
    }

    /// <summary>
    /// Extracts the per-widget slug from <c>TitleLocalizationKey</c> by stripping
    /// the <c>"Widget:{SourceDefinitionName}."</c> prefix when present, otherwise
    /// stripping just <c>"Widget:"</c>. The frontend uses the slug to address
    /// the widget in its <c>WidgetAction</c> dispatcher.
    /// </summary>
    private static string ExtractSlug(string titleLocalizationKey, string? sourceDefinitionName)
    {
        if (sourceDefinitionName is { Length: > 0 })
        {
            string prefix = $"Widget:{sourceDefinitionName}.";
            if (titleLocalizationKey.StartsWith(prefix, StringComparison.Ordinal))
            {
                return titleLocalizationKey[prefix.Length..];
            }
        }

        const string GenericPrefix = "Widget:";
        if (titleLocalizationKey.StartsWith(GenericPrefix, StringComparison.Ordinal))
        {
            return titleLocalizationKey[GenericPrefix.Length..];
        }

        return titleLocalizationKey;
    }
}
