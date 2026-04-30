using Granit.Events;

namespace Granit.Dashboards.Domain.Events;

/// <summary>Raised when a <see cref="Dashboard"/> is first created.</summary>
public sealed record DashboardCreatedEvent(Guid DashboardId, Guid? TenantId, string Name, string? SourceDefinitionName) : IDomainEvent;

/// <summary>Raised when a <see cref="Dashboard"/>'s metadata or layout changes.</summary>
public sealed record DashboardModifiedEvent(Guid DashboardId, Guid? TenantId) : IDomainEvent;

/// <summary>Raised when a <see cref="Dashboard"/> transitions to <see cref="DashboardStatus.Published"/>.</summary>
public sealed record DashboardPublishedEvent(Guid DashboardId, Guid? TenantId) : IDomainEvent;

/// <summary>Raised when a <see cref="Dashboard"/> transitions to <see cref="DashboardStatus.Archived"/>.</summary>
public sealed record DashboardArchivedEvent(Guid DashboardId, Guid? TenantId) : IDomainEvent;

/// <summary>
/// Raised when a <see cref="Dashboard"/> is resynced from its source definition (ADR-038 §3).
/// Carries the version delta and a coarse-grained widget churn summary so audit consumers
/// can reconcile dashboards that drifted between two module versions without re-reading
/// the persisted aggregate.
/// </summary>
public sealed record DashboardResyncedEvent(
    Guid DashboardId,
    Guid? TenantId,
    string? PreviousSourceDefinitionVersion,
    string NewSourceDefinitionVersion,
    int WidgetsAdded,
    int WidgetsRemoved,
    int OverridesCarriedOver) : IDomainEvent;
