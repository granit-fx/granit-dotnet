using System.Text.Json.Nodes;

namespace Granit.Entities.Views;

/// <summary>
/// Wire-shape projection of an <c>EntityView</c> aggregate, surfaced in the manifest's
/// <c>views</c> facet (ADR-047). Immutable — built from the aggregate by the read service.
/// </summary>
/// <param name="Id">Stable identifier of the saved view.</param>
/// <param name="EntityName">The entity's wire identifier (e.g. <c>"Granit.PM.Task"</c>).</param>
/// <param name="BasedOn">
/// Name of the compiled collection this view deltas over (per ADR-042). Immutable
/// post-creation — see ADR-047 §3.
/// </param>
/// <param name="Kind">View kind inherited from <paramref name="BasedOn"/> (per ADR-042).</param>
/// <param name="Name">User-facing label.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Icon">Optional icon name from the icon catalog.</param>
/// <param name="State">
/// JSONB delta over the base collection — filters, sort, columns, group, per-layout config.
/// Validated against the JSON Schema attached to <paramref name="Kind"/>.
/// </param>
/// <param name="Visibility">Personal / Shared / Tenant — see ADR-047 §4.</param>
/// <param name="OwnerId">User identifier of the creator; <see langword="null"/> for Tenant views.</param>
/// <param name="SharedWith">
/// Audience for <see cref="EntityViewVisibility.Shared"/> views; <see langword="null"/> otherwise.
/// </param>
/// <param name="IsPinned">Surfaced as an admin-pinned tab in the workspace tab strip.</param>
/// <param name="IsDefault">Replaces the compiled default for the tenant.</param>
/// <param name="IsPersonalDefault">User's landing view when navigating to the entity.</param>
/// <param name="SortOrder">Display order among views (lower first).</param>
public sealed record EntityViewDescriptor(
    Guid Id,
    string EntityName,
    string BasedOn,
    string Kind,
    string Name,
    string? Description,
    string? Icon,
    JsonObject State,
    EntityViewVisibility Visibility,
    Guid? OwnerId,
    EntityViewSharedWith? SharedWith,
    bool IsPinned,
    bool IsDefault,
    bool IsPersonalDefault,
    int SortOrder);
