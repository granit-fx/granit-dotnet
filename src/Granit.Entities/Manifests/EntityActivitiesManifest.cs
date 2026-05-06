namespace Granit.Entities.Manifests;

/// <summary>
/// Activities opt-in section of the per-entity manifest (ADR-046 §3). Present
/// only when the entity declared <c>.Activities()</c> on its
/// <c>EntityDefinitionBuilder</c> AND the host loaded
/// <c>Granit.Activities</c> runtime so the registry can validate type names.
/// </summary>
/// <param name="AllowedTypes">Activity type names the entity offers, after dropping any names absent from the runtime <c>IActivityRegistry</c> (per ADR-045 §3 silent-skip). Empty list = every registered type is allowed.</param>
/// <param name="DefaultAssignee">Optional property name (PascalCase) on the host entity that the React shell pre-fills as the assignee when creating an activity (e.g. <c>"AccountManagerUserId"</c>). Value resolution happens client-side from the loaded entity row.</param>
public sealed record EntityActivitiesManifest(
    IReadOnlyList<string> AllowedTypes,
    string? DefaultAssignee);
