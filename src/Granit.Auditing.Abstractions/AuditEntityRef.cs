namespace Granit.Auditing;

/// <summary>
/// Lightweight reference to an audited entity — the pair
/// (<see cref="EntityType"/>, <see cref="EntityId"/>) that the audit store
/// stamps on every <c>AuditEntityChange</c>. Used by the batch read path
/// (<see cref="IAuditingReader.GetByEntitiesAsync"/>) to request audits for
/// many entities in a single query.
/// </summary>
public readonly record struct AuditEntityRef(string EntityType, string EntityId);
