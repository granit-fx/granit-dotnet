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
