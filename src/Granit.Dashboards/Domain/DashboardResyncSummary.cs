namespace Granit.Dashboards.Domain;

/// <summary>
/// Result of <see cref="Dashboard.Resync"/>. Surfaced verbatim through the
/// <c>POST /dashboards/{id}/resync</c> response so admins can audit what the
/// resync actually changed without reading the diff against the persisted
/// aggregate.
/// </summary>
/// <param name="PreviousSourceDefinitionVersion">
/// Source-definition version captured on the dashboard before the resync ran.
/// Compared against <see cref="NewSourceDefinitionVersion"/> by the frontend's
/// drift banner to confirm the resync actually moved the dashboard's anchor.
/// </param>
/// <param name="NewSourceDefinitionVersion">
/// Source-definition version applied by the resync — pulled from the currently-registered
/// descriptor.
/// </param>
/// <param name="WidgetsAdded">Count of widgets the descriptor introduces and that did not exist on the persisted aggregate (matched by <c>TitleLocalizationKey</c>).</param>
/// <param name="WidgetsRemoved">Count of widgets the persisted aggregate carried but the descriptor no longer declares.</param>
/// <param name="OverridesCarriedOver">Count of widgets whose persisted <c>Overrides</c> were preserved on the new <see cref="WidgetInstance"/> via slug match. Best-effort — slugs the descriptor renamed silently lose their overrides.</param>
public sealed record DashboardResyncSummary(
    string? PreviousSourceDefinitionVersion,
    string NewSourceDefinitionVersion,
    int WidgetsAdded,
    int WidgetsRemoved,
    int OverridesCarriedOver);
