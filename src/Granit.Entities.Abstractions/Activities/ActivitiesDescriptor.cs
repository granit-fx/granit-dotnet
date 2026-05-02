namespace Granit.Entities.Activities;

/// <summary>
/// Immutable opt-in descriptor declaring that an
/// <see cref="EntityDefinition{TEntity}"/> hosts cross-entity activities
/// (ADR-046 §3). Built via
/// <see cref="ActivitiesOptionsBuilder{TEntity}"/> on the fluent builder.
/// </summary>
/// <param name="AllowedTypeNames">Activity type names the entity restricts itself to (e.g. <c>["Call", "Meeting", "Email", "ToDo"]</c>). Empty list means "every type registered with the framework". Names that don't resolve in <c>IActivityRegistry</c> are silently dropped at manifest time per ADR-045 §3 — so an entity that opts into <c>"Quote"</c> simply does not surface that option when <c>Granit.Sales</c> is not loaded.</param>
/// <param name="DefaultAssigneePropertyName">Optional property name (PascalCase) on the host entity that the React shell pre-fills as the assignee when creating an activity (e.g. <c>"AccountManagerUserId"</c>). The wire form is the property name, not the value — value resolution happens client-side from the loaded entity row.</param>
public sealed record ActivitiesDescriptor(
    IReadOnlyList<string> AllowedTypeNames,
    string? DefaultAssigneePropertyName);
