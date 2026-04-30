namespace Granit.Dashboards.Endpoints.Dtos;

/// <summary>
/// Wire shape for <c>POST /dashboards/{id}/resync</c>. Echoes the dashboard's
/// updated identifying fields alongside the change-summary so admins can audit
/// what the resync actually moved without re-reading the aggregate.
/// </summary>
/// <param name="Id">Persisted dashboard identifier (unchanged).</param>
/// <param name="Name">Dashboard name — preserved by the resync (admins keep their renames).</param>
/// <param name="Status">Dashboard status — preserved by the resync (a published dashboard stays published).</param>
/// <param name="SourceDefinitionName">Wire identifier of the source definition (unchanged).</param>
/// <param name="PreviousSourceDefinitionVersion">Source-definition version recorded on the dashboard before this resync ran.</param>
/// <param name="SourceDefinitionVersion">Source-definition version captured by the resync — pulled from the currently-registered descriptor.</param>
/// <param name="WidgetsAdded">Count of widgets the descriptor introduced (matched by <c>Widget:{Name}.{slug}</c>).</param>
/// <param name="WidgetsRemoved">Count of widgets the descriptor no longer declares.</param>
/// <param name="OverridesCarriedOver">Count of widgets whose persisted <c>Overrides</c> were preserved on the new instances via slug match. Slugs the descriptor renamed silently lose their overrides.</param>
public sealed record DashboardResyncResponse(
    Guid Id,
    string Name,
    Granit.Dashboards.Domain.DashboardStatus Status,
    string SourceDefinitionName,
    string? PreviousSourceDefinitionVersion,
    string SourceDefinitionVersion,
    int WidgetsAdded,
    int WidgetsRemoved,
    int OverridesCarriedOver);
