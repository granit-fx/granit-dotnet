namespace Granit.Dashboards.Endpoints.Dtos;

/// <summary>
/// Drift state of a persisted <c>Dashboard</c> against its source
/// <c>DashboardDefinition</c> at render time. ADR-038 §3 — surfaced on
/// <see cref="DashboardRenderResponse.DriftStatus"/> so the frontend can
/// warn admins or offer a resync action without round-tripping to a
/// dedicated drift-check endpoint.
/// </summary>
/// <remarks>
/// Comparison is semver-aware: persisted and registered <c>Version</c> strings
/// are parsed as <c>MAJOR.MINOR.PATCH</c> with an optional pre-release / build
/// suffix (<c>"1.2.3-alpha"</c>, <c>"1.2.3+build.7"</c>). The numeric tuple
/// drives <see cref="Behind"/> vs <see cref="Ahead"/>; mismatched suffixes or
/// unparseable strings collapse to <see cref="Unknown"/> (cautious default —
/// the frontend treats <see cref="Unknown"/> like <see cref="Aligned"/> for
/// resync prompts but surfaces a tooltip).
/// </remarks>
public enum DashboardDriftStatus
{
    /// <summary>
    /// The dashboard was custom-built — no source definition to drift against.
    /// Frontend hides drift UI entirely for these dashboards.
    /// </summary>
    NotApplicable = 0,

    /// <summary>
    /// Persisted <c>SourceDefinitionVersion</c> matches the currently-registered
    /// descriptor's <c>Version</c> on every semver component (MAJOR.MINOR.PATCH +
    /// pre-release / build suffix). No action required.
    /// </summary>
    Aligned = 1,

    /// <summary>
    /// Persisted <c>SourceDefinitionVersion</c> is older than the currently-registered
    /// descriptor — the source module shipped a newer version since import. Frontend
    /// should surface a "Dashboard outdated, click to resync" affordance; resync
    /// re-imports from the descriptor and best-effort preserves per-instance overrides
    /// via slug-matching.
    /// </summary>
    Behind = 2,

    /// <summary>
    /// Persisted <c>SourceDefinitionVersion</c> is newer than the currently-registered
    /// descriptor — the host loaded an older module than the one originally imported.
    /// Frontend should warn admins; resync would downgrade the dashboard, so offer
    /// it as an explicit "force-resync" rather than a one-click action.
    /// </summary>
    Ahead = 3,

    /// <summary>
    /// Versions cannot be ordered: either string fails the semver regex, or the
    /// numeric components match but pre-release / build suffixes differ
    /// (<c>"1.0.0-alpha"</c> vs <c>"1.0.0-beta"</c>). Frontend treats this like
    /// <see cref="Aligned"/> for the main banner but exposes a tooltip with both
    /// raw version strings so admins can reconcile manually.
    /// </summary>
    Unknown = 4,

    /// <summary>
    /// Source definition is no longer registered in the host (module unloaded,
    /// renamed, or removed). Resync is impossible until the source is re-shipped
    /// or the host wires up the missing module. Frontend should surface a
    /// terminal warning — the dashboard renders from persisted widgets only;
    /// view switches and per-widget Actions can no longer resolve.
    /// </summary>
    SourceUnregistered = 5,
}
