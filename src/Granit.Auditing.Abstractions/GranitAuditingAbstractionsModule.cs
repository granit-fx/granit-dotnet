using Granit.Modularity;
using Granit.QueryEngine;

namespace Granit.Auditing;

/// <summary>
/// Granit module for audit trail contracts: the audit domain model (AuditEntry,
/// AuditEntityChange, AuditPropertyChange), read/write/cleanup contracts
/// (IAuditingReader, IAuditingWriter, IAuditingCleaner), timeline aggregation
/// contracts, bus messages and the AuditEntryPersistedEto integration event.
/// </summary>
/// <remarks>
/// This module has no service registrations — it exists so that consumer modules
/// can declare <c>[DependsOn(typeof(GranitAuditingAbstractionsModule))]</c> and emit
/// or read audit entries without pulling in the Granit.Auditing runtime.
/// </remarks>
[DependsOn(typeof(GranitQueryEngineAbstractionsModule))]
public sealed class GranitAuditingAbstractionsModule : GranitModule;
