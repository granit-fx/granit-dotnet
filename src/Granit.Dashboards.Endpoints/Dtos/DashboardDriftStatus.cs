namespace Granit.Dashboards.Endpoints.Dtos;

/// <summary>
/// Drift state of a persisted <c>Dashboard</c> against its source
/// <c>DashboardDefinition</c> at render time. ADR-038 §3 — surfaced on
/// <see cref="DashboardRenderResponse.DriftStatus"/> so the frontend can
/// warn admins or offer a resync action without round-tripping to a
/// dedicated drift-check endpoint.
/// </summary>
public enum DashboardDriftStatus
{
    /// <summary>
    /// The dashboard was custom-built — no source definition to drift against.
    /// Frontend hides drift UI entirely for these dashboards.
    /// </summary>
    NotApplicable = 0,

    /// <summary>
    /// Persisted <c>SourceDefinitionVersion</c> matches the currently-registered
    /// descriptor's <c>Version</c>. No action required.
    /// </summary>
    InSync = 1,

    /// <summary>
    /// Persisted <c>SourceDefinitionVersion</c> differs from the currently-registered
    /// descriptor's <c>Version</c> — the source module shipped a newer (or older)
    /// version than what was imported. Frontend should surface a "Dashboard outdated,
    /// click to resync" affordance; resync re-imports from the descriptor and best-effort
    /// preserves per-instance overrides via slug-matching.
    /// </summary>
    Drift = 2,

    /// <summary>
    /// Source definition is no longer registered in the host (module unloaded,
    /// renamed, or removed). Resync is impossible until the source is re-shipped
    /// or the host wires up the missing module. Frontend should surface a
    /// terminal warning — the dashboard renders from persisted widgets only;
    /// view switches and per-widget Actions can no longer resolve.
    /// </summary>
    SourceUnregistered = 3,
}
