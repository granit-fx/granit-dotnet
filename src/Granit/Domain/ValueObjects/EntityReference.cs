namespace Granit.Domain;

/// <summary>
/// Polymorphic reference to a domain entity, used to link cross-module
/// concepts (notifications, timeline entries, audit logs) to their source entity.
/// </summary>
/// <param name="EntityType">Entity type name (e.g., "Invoice", "Patient").</param>
/// <param name="EntityId">Entity identifier as string (polymorphic).</param>
public sealed record EntityReference(string EntityType, string EntityId);
